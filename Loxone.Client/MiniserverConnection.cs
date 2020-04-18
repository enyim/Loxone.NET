// ----------------------------------------------------------------------
// <copyright file="MiniserverConnection.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Loxone.Client.Controls;
    using Loxone.Client.Transport;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Encapsulates connection to the Loxone Miniserver.
    /// </summary>
    public class MiniserverConnection : IDisposable, IEncryptorProvider, IConnection
    {
        private enum State
        {
            Constructed,
            Opening,
            Open,
            Disposing,
            Disposed
        }
        private volatile int _state;
        private Timer _keepAliveTimer;
        private Encryptor _requestOnlyEncryptor;
        private MiniserverAuthenticationMethod _authenticationMethod;
        private ICredentials _credentials;
        private Encryptor _requestAndResponseEncryptor;
        private static readonly Version _tokenAuthenticationThresholdVersion = new Version(9, 0);
        private Authenticator _authenticator;
        private CommandEncryption _defaultEncryption = CommandEncryption.None;
        private Task closedTask;
        // According to the documentation the Miniserver will close the connection if the
        // client doesn't send anything for more than 5 minutes.
        private static readonly TimeSpan _defaultKeepAliveTimeout = TimeSpan.FromMinutes(3);
        private TimeSpan _keepAliveTimeout = DefaultKeepAliveTimeout;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="address"></param>
        public MiniserverConnection(Uri address, ILogger logger)
        {
            MiniserverInfo = new MiniserverLimitedInfo();
            _state = (int)State.Constructed;
            Address = HttpUtils.MakeHttpUri(address);
            Logger = logger;
        }
        Task IConnection.ReceiveTask { get => closedTask; set =>closedTask=value; }
        public Task IsClosedAsync() => closedTask;
        public bool IsDisposed => _state >= (int)State.Disposing;
        public Uri Address { get; private set; }
        public CancellationTokenSource CtsConnection { get; private set; }
        public MiniserverLimitedInfo MiniserverInfo { get; }
        public event EventHandler<IReadOnlyList<TextState>> TextStateChanged;
        public event EventHandler<IReadOnlyList<ValueState>> ValueStateChanged;
        public Exception AnyException { get; private set; }
        public ICredentials Credentials
        {
            get => _credentials;
            set
            {
                CheckDisposed();
                CheckState(State.Constructed);
                _credentials = value;
            }
        }
        public static TimeSpan DefaultKeepAliveTimeout => _defaultKeepAliveTimeout;
        public MiniserverAuthenticationMethod AuthenticationMethod
        {
            get => _authenticationMethod;
            set
            {
                CheckDisposed();
                CheckState(State.Constructed);

                _authenticationMethod = value;
            }
        }
        public TimeSpan KeepAliveTimeout
        {
            get => _keepAliveTimeout;
            set
            {
                Contract.Requires(value > TimeSpan.Zero);
                CheckDisposed();
                _keepAliveTimeout = value;
            }
        }

        internal ILogger Logger { get; set; }
        internal LXWebSocket WebSocket { get; set; }
        internal Session Session { get; set; }
        internal MiniserverContext MiniserverContext { get; set; }

        void IEventListener.OnValueStateChanged(IReadOnlyList<ValueState> values) => ValueStateChanged?.Invoke(this, values);
        void IEventListener.OnTextStateChanged(IReadOnlyList<TextState> values) => TextStateChanged?.Invoke(this, values);

        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();
            CheckBeforeOpen();
            ChangeState(State.Opening, State.Constructed);

            // new cts for connection
            CtsConnection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                WebSocket = new LXWebSocket(HttpUtils.MakeWebSocketUri(Address), this, this, CtsConnection.Token);
                Session = new Session(WebSocket);
                await CheckMiniserverReachableAsync(CtsConnection.Token).ConfigureAwait(false);
                await OpenWebSocketAsync(CtsConnection.Token).ConfigureAwait(false);
                _authenticator = CreateAuthenticator();
                await _authenticator.AuthenticateAsync(CtsConnection.Token).ConfigureAwait(false);
                ChangeState(State.Open);
            }
            catch
            {
                ChangeState(State.Constructed);
                throw;
            }
        }



        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task<StructureFile> DownloadStructureFileAsync(CancellationToken cancellationToken)
        {
            CheckBeforeOperation();
            string s = await WebSocket.RequestStringAsync("data/LoxAPP3.json", cancellationToken).ConfigureAwait(false);
            return StructureFile.Parse(s);
        }

        public async Task<DateTime> GetStructureFileLastModifiedDateAsync(CancellationToken cancellationToken)
        {
            CheckBeforeOperation();
            var response = await WebSocket.RequestCommandAsync<DateTime>("jdev/sps/LoxAPPversion3", _defaultEncryption, cancellationToken).ConfigureAwait(false);
            return DateTime.SpecifyKind(response.Value, DateTimeKind.Local);
        }

        public async Task EnableStatusUpdatesAsync(CancellationToken cancellationToken)
        {
            CheckBeforeOperation();
            var response = await WebSocket.RequestCommandAsync<string>("jdev/sps/enablebinstatusupdate", _defaultEncryption, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<LXResponse<string>> Command(CancellationToken cancellation, Command command)
        {
            CheckBeforeOperation();
            var response = await WebSocket.RequestCommandAsync<string>($"jdev/sps/io/{command}", _defaultEncryption, cancellation).ConfigureAwait(false);
            return response;
        }

        void IErrorHandler.HandleError(Exception ex)
        {
            if(AnyException==null)AnyException = ex;
            if (ex is WebSocketException || ex is MiniserverTransportException)
            {
                CtsConnection.Cancel(); // cancel connection, thre is error anyway
            }
            else
            {
                Logger.LogWarning("In fire and forget: ",ex);
            }
        }


        private void CheckBeforeOperation()
        {
            CheckDisposed();
            CheckState(State.Open);
        }
        private void CheckBeforeOpen()
        {
            CheckDisposed();
            if (Address == null) throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.MiniserverConnection_MustBeSetBeforeOpenFmt, nameof(Address)));
            if (Credentials == null)
            {
                // Here we only check whether ICredentials is null. If ICredentials.GetCredential
                // returns null then the exception is thrown from OpenWebSocketAsync.
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.MiniserverConnection_MustBeSetBeforeOpenFmt, nameof(Credentials)));
            }
        }

        private async Task CheckMiniserverReachableAsync(CancellationToken cancellationToken)
        {
            var api = await WebSocket.CheckMiniserverReachableAsync(cancellationToken).ConfigureAwait(false);
            MiniserverInfo.Update(api);
        }

        private async Task OpenWebSocketAsync(CancellationToken cancellationToken)
        {
            await WebSocket.OpenAsync(cancellationToken).ConfigureAwait(false);
            StartKeepAliveTimer();
        }

        private void StartKeepAliveTimer()
        {
            Contract.Requires(_keepAliveTimer == null);
            _keepAliveTimer = new Timer(KeepAliveTimerTick, null, _keepAliveTimeout, Timeout.InfiniteTimeSpan);
        }

        private void KeepAliveTimerTick(object state)
        {
            if (!IsDisposed)
            {
                // TODO keep alive
            };
        }

        private Authenticator CreateAuthenticator()
        {
            var credentials = Credentials.GetCredential(Address, HttpUtils.BasicAuthenticationScheme);
            if (credentials == null) throw new InvalidOperationException();
            return CreateAuthenticator(credentials);
        }

        private Authenticator CreateAuthenticator(NetworkCredential credentials)
        {
            Contract.Requires(credentials != null);

            var method = AuthenticationMethod;

            if (method == MiniserverAuthenticationMethod.Default)
            {
                if (MiniserverInfo.FirmwareVersion < _tokenAuthenticationThresholdVersion) method = MiniserverAuthenticationMethod.Password;
                else method = MiniserverAuthenticationMethod.Token;
            }

            return method switch
            {
                MiniserverAuthenticationMethod.Password => new PasswordAuthenticator(Session, credentials),
                MiniserverAuthenticationMethod.Token => new TokenAuthenticator(Session, credentials),
                _ => throw new ArgumentOutOfRangeException(nameof(AuthenticationMethod)),
            };
        }

        private void CheckState(State requiredState)
        {
            if (_state != (int)requiredState) throw new InvalidOperationException();
        }

        private void ChangeState(State newState, State requiredState)
        {
            if (Interlocked.CompareExchange(ref _state, (int)newState, (int)requiredState) != (int)requiredState) throw new InvalidOperationException();
        }

        private State ChangeState(State newState) => (State)Interlocked.Exchange(ref _state, (int)newState);

        Encryptor IEncryptorProvider.GetEncryptor(CommandEncryption mode)
        {
            return mode switch
            {
                CommandEncryption.None => null,
                CommandEncryption.Request => LazyInitializer.EnsureInitialized(ref _requestOnlyEncryptor, () => new Encryptor(Session, CommandEncryption.Request)),
                CommandEncryption.RequestAndResponse => LazyInitializer.EnsureInitialized(ref _requestAndResponseEncryptor, () => new Encryptor(Session, CommandEncryption.RequestAndResponse)),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
        }

        #region IDisposable Implementation

        protected void CheckDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(this.GetType().FullName);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_state != (int)State.Disposed)
            {
                // May be already disposing on another thread.
                if (Interlocked.Exchange(ref _state, (int)State.Disposing) != (int)State.Disposing)
                {
                    ValueStateChanged = null;
                    TextStateChanged = null;

                    if (disposing)
                    {
                        _keepAliveTimer?.Dispose();
                        _authenticator?.Dispose();
                        Session?.Dispose();
                        WebSocket?.Dispose();
                    }
                    _state = (int)State.Disposed;
                }
            }
        }
        #endregion
    }
}
