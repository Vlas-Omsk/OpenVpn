using OpenVpn.Sessions.Packets;

namespace OpenVpn.Sessions
{
    /// <summary>
    /// Represents a session channel for processing OpenVPN session packets.
    /// Provides functionality for managing session-level communication and packet exchange.
    /// </summary>
    internal interface ISessionChannel : IDisposable
    {
        /// <summary>
        /// Writes a session packet to the channel. The packet is immediately processed and copied to the internal buffer.
        /// After this method returns, the packet buffer can be safely overwritten.
        /// </summary>
        /// <param name="packet">The session packet to write.</param>
        void Write(SessionPacket packet);

        /// <summary>
        /// Reads a session packet from the channel. The returned packet should be immediately processed before calling Receive(),
        /// because Receive() can override the previously read packet buffer.
        /// </summary>
        /// <returns>The session packet that was read, or null if no packet is available.</returns>
        SessionPacket? Read();

        /// <summary>
        /// Asynchronously sends any buffered session packets through the channel.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task Send(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously receives session packets from the channel and buffers them for reading.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous receive operation.</returns>
        Task Receive(CancellationToken cancellationToken);
    }
}
