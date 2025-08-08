using OpenVpn.Control.Packets;

namespace OpenVpn.Control
{
    /// <summary>
    /// Represents a control channel for processing OpenVPN control packets.
    /// Provides functionality for managing session connections, plugin integration, and packet communication.
    /// </summary>
    internal interface IControlChannel : IDisposable
    {
        /// <summary>
        /// Gets the session identifier for this control channel.
        /// </summary>
        ulong SessionId { get; }

        /// <summary>
        /// Gets the remote session identifier for this control channel.
        /// </summary>
        ulong RemoteSessionId { get; }

        /// <summary>
        /// Establishes a connection through the control channel.
        /// </summary>
        void Connect();

        /// <summary>
        /// Writes a control packet to the channel. The packet is immediately processed and copied to the internal buffer.
        /// After this method returns, the packet buffer can be safely overwritten.
        /// </summary>
        /// <param name="packet">The control packet to write.</param>
        void Write(IControlPacket packet);

        /// <summary>
        /// Reads a control packet from the channel. The returned packet should be immediately processed before calling Receive(),
        /// because Receive() can override the previously read packet buffer.
        /// </summary>
        /// <returns>The control packet that was read, or null if no packet is available.</returns>
        IControlPacket? Read();

        /// <summary>
        /// Asynchronously sends any buffered control packets through the channel.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task Send(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously receives control packets from the channel and buffers them for reading.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous receive operation.</returns>
        Task Receive(CancellationToken cancellationToken);
    }
}
