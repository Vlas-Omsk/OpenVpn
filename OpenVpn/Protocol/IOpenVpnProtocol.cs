using OpenVpn.Protocol.Packets;

namespace OpenVpn.Protocol
{
    /// <summary>
    /// Represents the main protocol interface for OpenVPN communication.
    /// Provides functionality for establishing connections, managing packet exchange, and handling protocol-level operations.
    /// </summary>
    public interface IOpenVpnProtocol : IDisposable
    {
        /// <summary>
        /// Establishes a connection through the OpenVPN protocol.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        Task Connect(CancellationToken cancellationToken);

        /// <summary>
        /// Writes a protocol packet to the channel. The packet is immediately processed and copied to the internal buffer.
        /// After this method returns, the packet buffer can be safely overwritten.
        /// </summary>
        /// <param name="packet">The protocol packet to write.</param>
        void Write(IOpenVpnProtocolPacket packet);

        /// <summary>
        /// Reads a protocol packet from the channel. The returned packet should be immediately processed before calling Receive(),
        /// because Receive() can override the previously read packet buffer.
        /// </summary>
        /// <returns>The protocol packet that was read, or null if no packet is available.</returns>
        IOpenVpnProtocolPacket? Read();

        /// <summary>
        /// Asynchronously sends any buffered protocol packets through the channel.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task Send(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously receives protocol packets from the channel and buffers them for reading.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous receive operation.</returns>
        Task Receive(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously waits for data to become available in the protocol channel.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous wait operation.</returns>
        Task WaitForData(CancellationToken cancellationToken);
    }
}
