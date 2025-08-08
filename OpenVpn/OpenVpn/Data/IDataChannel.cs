using OpenVpn.Data.Packets;

namespace OpenVpn.Data
{
    /// <summary>
    /// Represents a data channel for processing OpenVPN data packets.
    /// Provides functionality for reading, writing, sending, and receiving data packets.
    /// </summary>
    internal interface IDataChannel
    {
        /// <summary>
        /// Writes a data packet to the channel. The packet is immediately processed and copied to the internal buffer.
        /// After this method returns, the packet buffer can be safely overwritten.
        /// </summary>
        /// <param name="packet">The data packet to write.</param>
        void Write(IDataPacket packet);

        /// <summary>
        /// Reads a data packet from the channel. The returned packet should be immediately processed before calling Receive(),
        /// because Receive() can override the previously read packet buffer.
        /// </summary>
        /// <returns>The data packet that was read, or null if no packet is available.</returns>
        IDataPacket? Read();

        /// <summary>
        /// Asynchronously sends any buffered data packets through the channel.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        Task Send(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously receives data packets from the channel and buffers them for reading.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous receive operation.</returns>
        Task Receive(CancellationToken cancellationToken);
    }
}
