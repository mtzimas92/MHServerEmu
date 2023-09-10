﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Common.Extensions;
using MHServerEmu.GameServer.Common;
using MHServerEmu.GameServer.GameData;

namespace MHServerEmu.GameServer.Entities.Locomotion
{
    public class LocomotionStateUpdateArchive
    {
        private const int LocFlagCount = 16;

        public uint ReplicationPolicy { get; set; }
        public ulong EntityId { get; set; }
        public bool[] LocFlags { get; set; }
        public ulong PrototypeId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Orientation { get; set; }
        public LocomotionState LocomotionState { get; set; }

        public LocomotionStateUpdateArchive(byte[] data)
        {
            CodedInputStream stream = CodedInputStream.CreateInstance(data);

            ReplicationPolicy = stream.ReadRawVarint32();
            EntityId = stream.ReadRawVarint64();
            LocFlags = stream.ReadRawVarint32().ToBoolArray(LocFlagCount);
            if (LocFlags[11]) PrototypeId = stream.ReadPrototypeId(PrototypeEnumType.Entity);
            Position = new(stream, 3);
            if (LocFlags[0])
                Orientation = new(stream, 6);
            else
                Orientation = new(stream.ReadRawFloat(6), 0f, 0f);
            LocomotionState = new(stream, LocFlags);
        }

        public LocomotionStateUpdateArchive() { }

        public byte[] Encode()
        {
            using (MemoryStream memoryStream = new())
            {
                CodedOutputStream stream = CodedOutputStream.CreateInstance(memoryStream);

                stream.WriteRawVarint32(ReplicationPolicy);
                stream.WriteRawVarint64(EntityId);
                stream.WriteRawVarint32(LocFlags.ToUInt32());
                if (LocFlags[11]) stream.WritePrototypeId(PrototypeId, PrototypeEnumType.Entity);
                stream.WriteRawBytes(Position.Encode());
                if (LocFlags[0])
                    stream.WriteRawBytes(Orientation.Encode(6));
                else
                    stream.WriteRawFloat(Orientation.X, 6);
                stream.WriteRawBytes(LocomotionState.Encode(LocFlags));

                stream.Flush();
                return memoryStream.ToArray();
            }
        }

        public override string ToString()
        {
            using (MemoryStream stream = new())
            using (StreamWriter writer = new(stream))
            {
                writer.WriteLine($"ReplicationPolicy: 0x{ReplicationPolicy:X}");
                writer.WriteLine($"EntityId: 0x{EntityId:X}");
                for (int i = 0; i < LocFlags.Length; i++) writer.WriteLine($"LocFlag{i}: {LocFlags[i]}");
                writer.WriteLine($"PrototypeId: 0x{PrototypeId:X}");
                writer.WriteLine($"Position: {Position}");
                writer.WriteLine($"Orientation: {Orientation}");
                writer.WriteLine($"LocomotionState: {LocomotionState}");

                writer.Flush();
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

    }
}
