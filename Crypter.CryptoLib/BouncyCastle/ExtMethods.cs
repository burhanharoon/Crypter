﻿using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using System.IO;

namespace Crypter.CryptoLib.BouncyCastle
{
   public static class ExtMethods
   {
      public static string ConvertToPEM(this AsymmetricKeyParameter keyParams)
      {
         var stringWriter = new StringWriter();
         var pemWriter = new PemWriter(stringWriter);
         pemWriter.WriteObject(keyParams);
         pemWriter.Writer.Flush();
         return stringWriter.ToString();
      }

      public static AsymmetricCipherKeyPair ConvertFromPEM(string pemKey)
      {
         var stringReader = new StringReader(pemKey);
         var pemReader = new PemReader(stringReader);
         return (AsymmetricCipherKeyPair)pemReader.ReadObject();
      }

      public static byte[] ConvertToBytes(this KeyParameter symmetricKey)
      {
         return symmetricKey.GetKey();
      }
   }
}
