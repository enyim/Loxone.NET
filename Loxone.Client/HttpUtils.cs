// ----------------------------------------------------------------------
// <copyright file="HttpUtils.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net.Http.Headers;
    using Loxone.Client.Transport;

    internal static class HttpUtils
    {
        public const int DefaultHttpPort = 80;

        public const string HttpScheme = "http"; // Default

        public const string HttpsScheme = "https";

        public const string WebSocketScheme = "ws";

        public const string JsonMediaType = "application/json";

        public const string BasicAuthenticationScheme = "Basic";

        public static bool IsHttpUri(Uri uri)
        {
            Contract.Requires(uri != null);
            return string.Equals(HttpScheme, uri.Scheme, StringComparison.OrdinalIgnoreCase) || string.Equals(HttpsScheme, uri.Scheme, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWebSocketUri(Uri uri)
        {
            Contract.Requires(uri != null);
            return string.Equals(WebSocketScheme, uri.Scheme, StringComparison.OrdinalIgnoreCase);
        }

        private static Uri ChangeScheme(Uri uri, string scheme)
        {
            Contract.Requires(uri != null);
            if (!String.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)) uri = new UriBuilder(uri) {
                Scheme = scheme,
                Port = uri.Port,
                Host = uri.Host,
            }.Uri;
            return uri;
        }

        public static Uri MakeWebSocketUri(LXUri uri) => new Uri($"ws://{uri.UriBase}:{uri.PortWs}/");

        public static Uri MakeHttpUri(LXUri uri) => new Uri($"{uri.Scheme}://{uri.UriBase}:{uri.PortHttp}/");

        public static bool IsJsonMediaType(MediaTypeHeaderValue mediaType)
        {
            return mediaType != null && string.Equals(mediaType.MediaType, JsonMediaType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
