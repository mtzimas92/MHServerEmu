﻿namespace MHServerEmu.Common
{
    // Below are placeholder bit field enums we can use until we figure out appropriate flags for each case

    [Flags]
    public enum UInt8Flags : ushort
    {
        None = 0,
        Flag0 = 1,
        Flag1 = 2,
        Flag2 = 4,
        Flag3 = 8,
        Flag4 = 16,
        Flag5 = 32,
        Flag6 = 64,
        Flag7 = 128
    }

    [Flags]
    public enum UInt16Flags : ushort
    {
        None = 0,
        Flag0 = 1,
        Flag1 = 2,
        Flag2 = 4,
        Flag3 = 8,
        Flag4 = 16,
        Flag5 = 32,
        Flag6 = 64,
        Flag7 = 128,
        Flag8 = 256,
        Flag9 = 512,
        Flag10 = 1024,
        Flag11 = 2048,
        Flag12 = 4096,
        Flag13 = 8192,
        Flag14 = 16384,
        Flag15 = 32768
    }

    [Flags]
    public enum UInt32Flags : uint
    {
        None = 0,
        Flag0 = 1,
        Flag1 = 2,
        Flag2 = 4,
        Flag3 = 8,
        Flag4 = 16,
        Flag5 = 32,
        Flag6 = 64,
        Flag7 = 128,
        Flag8 = 256,
        Flag9 = 512,
        Flag10 = 1024,
        Flag11 = 2048,
        Flag12 = 4096,
        Flag13 = 8192,
        Flag14 = 16384,
        Flag15 = 32768,
        Flag16 = 65536,
        Flag17 = 131072,
        Flag18 = 262144,
        Flag19 = 524288,
        Flag20 = 1048576,
        Flag21 = 2097152,
        Flag22 = 4194304,
        Flag23 = 8388608,
        Flag24 = 16777216,
        Flag25 = 33554432,
        Flag26 = 67108864,
        Flag27 = 134217728,
        Flag28 = 268435456,
        Flag29 = 536870912,
        Flag30 = 1073741824,
        Flag31 = 2147483648
    }

    [Flags]
    public enum UInt64Flags : ulong
    {
        None = 0,
        Flag0 = 1,
        Flag1 = 2,
        Flag2 = 4,
        Flag3 = 8,
        Flag4 = 16,
        Flag5 = 32,
        Flag6 = 64,
        Flag7 = 128,
        Flag8 = 256,
        Flag9 = 512,
        Flag10 = 1024,
        Flag11 = 2048,
        Flag12 = 4096,
        Flag13 = 8192,
        Flag14 = 16384,
        Flag15 = 32768,
        Flag16 = 65536,
        Flag17 = 131072,
        Flag18 = 262144,
        Flag19 = 524288,
        Flag20 = 1048576,
        Flag21 = 2097152,
        Flag22 = 4194304,
        Flag23 = 8388608,
        Flag24 = 16777216,
        Flag25 = 33554432,
        Flag26 = 67108864,
        Flag27 = 134217728,
        Flag28 = 268435456,
        Flag29 = 536870912,
        Flag30 = 1073741824,
        Flag31 = 2147483648,
        Flag32 = 4294967296,
        Flag33 = 8589934592,
        Flag34 = 17179869184,
        Flag35 = 34359738368,
        Flag36 = 68719476736,
        Flag37 = 137438953472,
        Flag38 = 274877906944,
        Flag39 = 549755813888,
        Flag40 = 1099511627776,
        Flag41 = 2199023255552,
        Flag42 = 4398046511104,
        Flag43 = 8796093022208,
        Flag44 = 17592186044416,
        Flag45 = 35184372088832,
        Flag46 = 70368744177664,
        Flag47 = 140737488355328,
        Flag48 = 281474976710656,
        Flag49 = 562949953421312,
        Flag50 = 1125899906842624,
        Flag51 = 2251799813685248,
        Flag52 = 4503599627370496,
        Flag53 = 9007199254740992,
        Flag54 = 18014398509481984,
        Flag55 = 36028797018963968,
        Flag56 = 72057594037927936,
        Flag57 = 144115188075855872,
        Flag58 = 288230376151711744,
        Flag59 = 576460752303423488,
        Flag60 = 1152921504606846976,
        Flag61 = 2305843009213693952,
        Flag62 = 4611686018427387904,
        Flag63 = 9223372036854775808
    }
}
