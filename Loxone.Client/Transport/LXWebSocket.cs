// ----------------------------------------------------------------------
// <copyright file="LXWebSocket.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Transport
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class LXWebSocket : LXClient
    {
        private ClientWebSocket _webSocket; // TODO maybe reusable static
        private CancellationTokenSource _receiveLoopCancellation;
        private ICommandHandler _pendingCommand;
        private LXHttpClient _httpClient; //another http client
        private readonly IEncryptorProvider _encryptorProvider;
        private readonly IConnection _connection;
        private TimeSpan keepAlive;

        protected internal override LXClient HttpClient => _httpClient;

        public LXWebSocket(LXUri baseUri, IEncryptorProvider encryptorProvider, IConnection connection,CancellationToken ct) : base(HttpUtils.MakeWebSocketUri(baseUri), ct)
        {
            this._encryptorProvider = encryptorProvider;
            this._connection = connection;
            this._httpClient = new LXHttpClient(HttpUtils.MakeHttpUri(baseUri), ct);
        }


        public void SetKeepAlive(TimeSpan timeSpan)
        {
            keepAlive = timeSpan;
        }

        public async Task CloseAsync()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);
            await _webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Cancellation requested",cts.Token);
        }

        protected override async Task OpenInternalAsync(CancellationToken cancellationToken)
        {
            Contract.Requires(_webSocket == null);
            _webSocket = new ClientWebSocket();
            _webSocket.Options.KeepAliveInterval = keepAlive;
            _webSocket.Options.AddSubProtocol("remotecontrol"); // Probably should check scheme for http/https
            await _webSocket.ConnectAsync(new UriBuilder(BaseUri) { Path = "ws/rfc6455" }.Uri, cancellationToken).ConfigureAwait(false);
            StartReceiveLoop();
        }

        public void StartReceiveLoop()
        {
            Contract.Requires(_receiveLoopCancellation == null, "Receive loop is already running");
            _receiveLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(ConnectionToken);
            _connection.ReceiveTask = ReceiveLoopAsync(); // assigned to variable for checking
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (true)
                {
                    if (_receiveLoopCancellation == null) throw new MiniserverTransportException("Receive loop is not running");
                    if (_receiveLoopCancellation.IsCancellationRequested) throw new MiniserverTransportException("Receive loop is cancelled");
                    var header = await ReceiveHeaderAsync(_receiveLoopCancellation.Token).ConfigureAwait(false);
                    Contract.Assert(!header.IsLengthEstimated);
                    await DispatchMessageAsync(header, _receiveLoopCancellation.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                await CloseAsync();
            }
        }

        private async Task DispatchMessageAsync(MessageHeader header, CancellationToken cancellationToken)
        {
            bool processed = false;

            var command = _pendingCommand;
            if (command != null && command.CanHandleMessage(header.Identifier))
            {
                Interlocked.CompareExchange(ref _pendingCommand, null, command);
                await command.HandleMessageAsync(header, this, cancellationToken).ConfigureAwait(false);
                processed = true;
                (command as IDisposable)?.Dispose();
            }

            if (!processed)
            {
                processed = await HandleUnsolicitedMessageAsync(header, cancellationToken).ConfigureAwait(false);
                if (!processed)
                {
                    await SkipBytesAsync(header.Length, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<bool> HandleUnsolicitedMessageAsync(MessageHeader header, CancellationToken cancellationToken)
        {
            switch (header.Identifier)
            {
                case MessageIdentifier.ValueStates:
                    await HandleValueStatesAsync(header.Length, cancellationToken).ConfigureAwait(false);
                    return true;

                case MessageIdentifier.TextStates:
                    await HandleTextStatesAsync(header.Length, cancellationToken).ConfigureAwait(false);
                    return true;

                case MessageIdentifier.DayTimerStates:
                    break;

                case MessageIdentifier.OutOfService:
                    break;

                case MessageIdentifier.KeepAlive:
                    break;

                case MessageIdentifier.WeatherStates:
                    break;
            }

            return false;
        }

        private async Task HandleValueStatesAsync(int length, CancellationToken cancellationToken)
        {
            if (length >= 24)
            {
                var states = new List<ValueState>(length / 24);
                var buffer = new ArraySegment<byte>(new byte[24]);

                while (length >= 24)
                {
                    await ReceiveAtomicAsync(buffer, false, cancellationToken).ConfigureAwait(false);
                    var uuid = new Uuid(new ArraySegment<byte>(buffer.Array, 0, 16));
                    double value = BitConverter.ToDouble(buffer.Array, 16);
                    states.Add(new ValueState(uuid, value));
                    length -= 24;
                }

                _connection.OnValueStateChanged(states);
            }

            if (length > 0)
            {
                // Extra data beyond the last value state?
                await SkipBytesAsync(length, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task HandleTextStatesAsync(int length, CancellationToken cancellationToken)
        {
            if (length >= 36)
            {
                var states = new List<TextState>();
                var buffer = new ArraySegment<byte>(new byte[length]);
                await ReceiveAtomicAsync(buffer, false, cancellationToken).ConfigureAwait(false);
                int offset = 0;
                while (length >= 36)
                {
                    int processed = ParseTextState(buffer.Array, ref offset, out var state);
                    states.Add(state);
                    Contract.Assert(processed <= length);
                    length -= processed;
                }

                _connection.OnTextStateChanged(states);
            }

            if (length > 0)
            {
                // Extra data beyond the last text state?
                await SkipBytesAsync(length, cancellationToken).ConfigureAwait(false);
            }
        }

        private static int ParseTextState(byte[] bytes, ref int offset, out TextState state)
        {
            Uuid control = new Uuid(new ArraySegment<byte>(bytes, offset +  0, 16));
            Uuid icon    = new Uuid(new ArraySegment<byte>(bytes, offset + 16, 16));
            int  length  = BitConverter.ToInt32(           bytes, offset + 32);
            string text = String.Empty;
            if (length > 0)
            {
                text = LXClient.Encoding.GetString(        bytes, offset + 36, length);
                length = 4 * ((length - 1) / 4 + 1);
            }
            state = new TextState(control, icon, text);
            int processed = 36 + length;
            offset += processed;
            return processed;
        }

        private async Task SkipBytesAsync(int numberOfBytesToReadOut, CancellationToken cancellationToken)
        {
            int bufferSize = Math.Min(numberOfBytesToReadOut, 4096);
            var buffer = new ArraySegment<byte>(new byte[bufferSize]);

            while (numberOfBytesToReadOut > 0)
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                numberOfBytesToReadOut -= result.Count;

                if (result.CloseStatus != null)
                {
                    break;
                }
            }
        }

        internal async Task ReceiveAtomicAsync(ArraySegment<byte> segment, bool throwIfNotEom, CancellationToken cancellationToken)
        {
            int received = 0;
            bool eom = false;

            while (received < segment.Count && !cancellationToken.IsCancellationRequested)
            {
                var slice = new ArraySegment<byte>(segment.Array, segment.Offset + received, segment.Count - received);
                var result = await _webSocket.ReceiveAsync(slice, cancellationToken).ConfigureAwait(false);
                received += result.Count;

                if (result.CloseStatus != null) throw new WebSocketException((int)result.CloseStatus.Value, "Closed by other party");
                eom = result.EndOfMessage;
            }

            if (received < segment.Count) throw new WebSocketException(WebSocketError.Faulted);
            else if (received == segment.Count && !eom && throwIfNotEom)
            {
                // More data available but unexpected.
                throw new MiniserverTransportException();
            }
        }

        private async Task<MessageHeader> ReceiveHeaderAsync(CancellationToken cancellationToken)
        {
            var headerSegment = new ArraySegment<byte>(new byte[8]);
            await ReceiveAtomicAsync(headerSegment, false, cancellationToken).ConfigureAwait(false);
            var header = new MessageHeader(headerSegment);
            if (header.IsLengthEstimated)
            {
                await ReceiveAtomicAsync(headerSegment, false, cancellationToken).ConfigureAwait(false);
                header = new MessageHeader(headerSegment);
            }

            return header;
        }


        #region Sending
        //public async Task KeepAliveAsync(CancellationToken cancellation)
        //{
        //    var handler = new LXResponseCommandHandler<string>();
        //    if (Interlocked.CompareExchange(ref _pendingCommand, handler, null) == null)
        //    {
        //        // No command pending
        //        var header = new ArraySegment<byte>(new byte[8]);
        //        header[0] = 0x03;
        //        header[1] = (byte)MessageIdentifier.KeepAlive;
        //        //header[2] = (byte)MessageInfoFlags.None;
        //        await _webSocket.SendAsync(header, WebSocketMessageType.Binary, false, cancellation);
        //    }
        //}

        public async Task<string> RequestStringAsync(string command, CancellationToken cancellationToken)
        {
            var handler = new StringCommandHandler();
            await EnqueueCommandAsync(command, handler, cancellationToken).ConfigureAwait(false);
            var s = await handler.Task.ConfigureAwait(false);
            return s;
        }
        protected override async Task<LXResponse<T>> RequestCommandInternalAsync<T>(string command, CommandEncryption encryption, CancellationToken cancellationToken)
        {
            var handler = new LXResponseCommandHandler<T>();
            ApplyEncryption(handler, encryption);
            await EnqueueCommandAsync(command, handler, cancellationToken).ConfigureAwait(false);
            return await handler.Task.ConfigureAwait(false);
        }

        private async Task EnqueueCommandAsync(string command, ICommandHandler handler, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _pendingCommand, handler, null) != null)
            {
                // Command already pending.
                throw new InvalidOperationException();
            }

            if (handler.Encoder != null) command = handler.Encoder.EncodeCommand(command);
            await SendStringAsync(command, cancellationToken).ConfigureAwait(false);
        }

        private Task SendStringAsync(string s, CancellationToken cancellationToken)
        {
            var encoded = Encoding.GetBytes(s);
            var segment = new ArraySegment<byte>(encoded);
            return _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
        }

        private void ApplyEncryption<T>(LXResponseCommandHandler<T> handler, CommandEncryption encryption)
        {
            Encryptor encryptor = _encryptorProvider.GetEncryptor(encryption);
            if (encryption != CommandEncryption.Request)
            {
                handler.Encoder = encryptor;
                if (encryption == CommandEncryption.RequestAndResponse)
                {
                    handler.Decoder = encryptor;
                }
            }
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_receiveLoopCancellation != null)
                {
                    _receiveLoopCancellation.Cancel();
                    _receiveLoopCancellation.Dispose();
                }

                _httpClient?.Dispose();
                _webSocket?.Dispose();
            }
        }
    }
}
