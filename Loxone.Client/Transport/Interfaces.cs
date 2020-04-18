
namespace Loxone.Client.Transport
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System;

    internal interface ICommandHandler
    {
        IRequestEncoder Encoder { get; }

        IResponseDecoder Decoder { get; }

        bool CanHandleMessage(MessageIdentifier identifier);

        Task HandleMessageAsync(MessageHeader header, LXWebSocket socket, CancellationToken cancellationToken);
    }

    internal interface IEncryptorProvider
    {
        Encryptor GetEncryptor(CommandEncryption mode);
    }


    internal interface IEventListener
    {
        void OnValueStateChanged(IReadOnlyList<ValueState> values);

        void OnTextStateChanged(IReadOnlyList<TextState> values);
    }

    internal interface IErrorHandler
    {
        void HandleError(Exception ex);
    }

    internal interface IConnection : IEventListener, IErrorHandler
    {
        Task ReceiveTask { get; internal set; }
    }


    internal interface IRequestEncoder
    {
        string EncodeCommand(string command);
    }

    internal interface IResponseDecoder
    {
        string DecodeCommand(string command);
    }
}
