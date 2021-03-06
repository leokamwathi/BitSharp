﻿using System;
using System.Collections.Immutable;

namespace BitSharp.Node.Domain
{
    public class NetworkAddress
    {
        public readonly UInt64 Services;
        public readonly ImmutableArray<byte> IPv6Address;
        public readonly UInt16 Port;

        public NetworkAddress(UInt64 Services, ImmutableArray<byte> IPv6Address, UInt16 Port)
        {
            this.Services = Services;
            this.IPv6Address = IPv6Address;
            this.Port = Port;
        }

        public NetworkAddress With(UInt64? Services = null, ImmutableArray<byte>? IPv6Address = null, UInt16? Port = null)
        {
            return new NetworkAddress
            (
                Services ?? this.Services,
                IPv6Address ?? this.IPv6Address,
                Port ?? this.Port
            );
        }
    }
}
