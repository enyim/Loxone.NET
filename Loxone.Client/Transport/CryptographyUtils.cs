// ----------------------------------------------------------------------
// <copyright file="CryptographyUtils.cs">
//     Copyright (c) The Loxone.NET Authors.  All rights reserved.
// </copyright>
// <license>
//     Use of this source code is governed by the MIT license that can be
//     found in the LICENSE.txt file.
// </license>
// ----------------------------------------------------------------------

namespace Loxone.Client.Transport
{
    using System.Security.Cryptography;

    internal static class CryptographyUtils
    {
        public static RSA GetRsaPublicKey(byte[] der)
        {
            var rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(der, out _);

            }catch(CryptographicException e)
            {
                rsa.Dispose();
                throw new MiniserverException("InvalidCertificateX.509-ASN.1-DER", e);
            }

            return rsa;
        } 
    }
}
