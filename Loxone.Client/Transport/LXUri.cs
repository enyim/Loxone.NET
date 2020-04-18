// ----------------------------------------------------------------------
// <copyright file="ValueState.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Transport
{
    public readonly struct LXUri
    {
        public string Scheme {get;}
        public string UriBase { get; }

        public int PortHttp { get; }

        public int PortWs { get; }

        public LXUri(string scheme, string uriBase, int portHttp = 80, int portWs = 80)
        {

            Scheme = scheme;
            UriBase = uriBase;
            PortHttp = portHttp;
            PortWs = portWs;
        }

        public override string ToString() => $"{Scheme}://{UriBase}:{PortHttp}->{PortWs}";
    }
}
