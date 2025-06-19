using ABXClient.model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ABXClient
{
    public class AbxClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _outputFilePath;

        public AbxClient(string host, int port, string outputFilePath)
        {
            _host = host;
            _port = port;
            _outputFilePath = outputFilePath;
        }

        public void Run()
        {
            try
            {
                var packets = GetAllPackets();
                if (packets == null || packets.Count == 0)
                {
                    return;
                }
                WritePacketsToJson(packets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private List<Packet> GetAllPackets()
        {
            try
            {
                var packets = new Dictionary<int, Packet>();

                using (var client = new TcpClient())
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    client.Connect(_host, _port);
                    using (var stream = client.GetStream())
                    {
                        SendStreamAllPacketsRequest(stream);
                        var initialPackets = ReadPackets(stream);
                        foreach (var packet in initialPackets)
                        {
                            packets[packet.PacketSequence] = packet;
                        }
                    }
                }

                var missingSequences = FindMissingSequences(packets.Keys.ToList());
                //Console.WriteLine($"missing sequences: {string.Join(", ", missingSequences)}");
                foreach (var seq in missingSequences)
                {
                    var packet = GetPacketBySequence(seq);
                    if (packet != null)
                    {
                        packets[seq] = packet;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to retrieve packet for sequence {seq}.");
                    }
                }
                var result = packets.Values.OrderBy(p => p.PacketSequence).ToList();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPackets: {ex.Message}");
                return new List<Packet>();
            }
        }

        private void SendStreamAllPacketsRequest(NetworkStream stream)
        {
            byte[] request = { 0x01, 0x00 };
            stream.Write(request, 0, request.Length);
            Console.WriteLine("Sent request for all packets.");
        }

        private void SendResendPacketRequest(NetworkStream stream, int sequence)
        {
            byte[] request = { 0x02, (byte)sequence };
            stream.Write(request, 0, request.Length);
            Console.WriteLine($"Sent request to resend packet with sequence {sequence}.");
        }

        private List<Packet> ReadPackets(NetworkStream stream)
        {
            var packets = new List<Packet>();
            var buffer = new byte[17];

            try
            {
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Server closed connection.");
                        break;
                    }
                    if (bytesRead != 17)
                    {
                        continue;
                    }

                    var packet = ParsePacket(buffer);
                    packets.Add(packet);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"IO Exception: {ex.Message}");
            }

            return packets;
        }

        private Packet ParsePacket(byte[] buffer)
        {
            return new Packet
            {
                Symbol = Encoding.ASCII.GetString(buffer, 0, 4).TrimEnd('\0'),
                BuySellIndicator = (char)buffer[4],
                Quantity = ReadInt32BigEndian(buffer, 5),
                Price = ReadInt32BigEndian(buffer, 9),
                PacketSequence = ReadInt32BigEndian(buffer, 13)
            };
        }

        private int ReadInt32BigEndian(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) |
                   (buffer[offset + 1] << 16) |
                   (buffer[offset + 2] << 8) |
                   buffer[offset + 3];
        }

        private List<int> FindMissingSequences(List<int> sequences)
        {
            if (!sequences.Any()) return new List<int>();

            //var minSeq = sequences.Min();
            var maxSeq = sequences.Max();
            var missing = new List<int>();

            for (int i = 1; i <= maxSeq; i++)
            {
                if (!sequences.Contains(i))
                {
                    missing.Add(i);
                }
            }

            return missing;
        }

        private Packet GetPacketBySequence(int sequence)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    client.Connect(_host, _port);
                    using (var stream = client.GetStream())
                    {
                        SendResendPacketRequest(stream, sequence);
                        var packets = ReadPackets(stream);
                        var packet = packets.FirstOrDefault(p => p.PacketSequence == sequence);
                        if (packet == null)
                        {
                            Console.WriteLine($"No packet received for sequence {sequence}.");
                        }
                        return packet;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching packet {sequence}: {ex.Message}");
                return null;
            }
        }

        private void WritePacketsToJson(List<Packet> packets)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(packets, options);

                var directory = Path.GetDirectoryName(_outputFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_outputFilePath, json);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"IO file Error on writing JSON file: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on writing JSON: {ex.Message}");
                throw;
            }
        }
    }
}