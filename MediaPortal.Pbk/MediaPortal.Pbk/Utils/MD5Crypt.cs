//
// Oct-07 - Martin Fernández translation for all the linux fans and detractors...
//  
/*
#########################################################
# md5crypt.py
#
# 0423.2000 by michal wallace http://www.sabren.com/
# based on perl's Crypt::PasswdMD5 by Luis Munoz (lem@cantv.net)
# based on /usr/src/libcrypt/crypt.c from FreeBSD 2.2.5-RELEASE
#
# MANY THANKS TO
#
#  Carey Evans - http://home.clear.net.nz/pages/c.evans/
#  Dennis Marti - http://users.starpower.net/marti1/
#
#  For the patches that got this thing working!
#
#########################################################
md5crypt.py - Provides interoperable MD5-based crypt() function

SYNOPSIS

    import md5crypt.py

    cryptedpassword = md5crypt.md5crypt(password, salt);

DESCRIPTION

unix_md5_crypt() provides a crypt()-compatible interface to the
rather new MD5-based crypt() function found in modern operating systems.
It's based on the implementation found on FreeBSD 2.2.[56]-RELEASE and
contains the following license in it:

 "THE BEER-WARE LICENSE" (Revision 42):
 <phk@login.dknet.dk> wrote this file.  As long as you retain this notice you
 can do whatever you want with this stuff. If we meet some day, and you think
 this stuff is worth it, you can buy me a beer in return.   Poul-Henning Kamp

apache_md5_crypt() provides a function compatible with Apache's
.htpasswd files. This was contributed by Bryan Hart <bryan@eai.com>.
*/

using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MediaPortal.Pbk.Utils
{
    public class MD5Crypt
    {
        /** Password hash magic */
        private const string _MAGIC = "$1$";

        /** Characters for base64 encoding */
        private const string _BASE64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Generaate random salt
        /// </summary>
        /// <param name="iLength">length of the salt</param>
        /// <returns>salt</returns>
        public static string GetRandomSalt(int iLength = 8)
        {
            char[] salt = new char[iLength];

            Random random = new Random();

            for (int i = 0; i < iLength; i++)
                salt[i] = _BASE64[random.Next(_BASE64.Length)];

            return new String(salt);
        }

        /// <summary>
        /// A function to concatenate bytes[]
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns>New adition array</returns>
        private static byte[] _Concat(byte[] array1, byte[] array2)
        {
            byte[] concat = new byte[array1.Length + array2.Length];
            System.Buffer.BlockCopy(array1, 0, concat, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, concat, array1.Length, array2.Length);
            return concat;
        }

        /// <summary>
        /// Another function to concatenate bytes[]
        /// </summary>
        /// <param name="array1"></param>
        /// <param name="array2"></param>
        /// <returns>New adition array</returns>
        private static byte[] _PartialConcat(byte[] array1, byte[] array2, int iMax)
        {
            byte[] concat = new byte[array1.Length + iMax];
            System.Buffer.BlockCopy(array1, 0, concat, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, concat, array1.Length, iMax);
            return concat;
        }

        /// <summary>
        /// Base64-Encode integer value
        /// </summary>
        /// <param name="iValue"> The value to encode</param>
        /// <param name="iLength"> Desired length of the result</param>
        /// <returns>@return Base64 encoded value</returns>
        private static string _ToBase64(int iValue, int iLength)
        {
            StringBuilder result = new StringBuilder();
            _ToBase64(result, iValue, iLength);
            return result.ToString();
        }
        /// <summary>
        /// Base64-Encode integer value
        /// </summary>
        /// <param name="sb"> The output stringbuilder</param>
        /// <param name="iValue"> The value to encode</param>
        /// <param name="iLength"> Desired length of the result</param>
        /// <returns>@return Base64 encoded value</returns>
        private static void _ToBase64(StringBuilder sb, int iValue, int iLength)
        {
            while (--iLength >= 0)
            {
                sb.Append(_BASE64.Substring(iValue & 0x3f, 1));
                iValue >>= 6;
            }
        }

        /// <summary>
        /// Unix-like Crypt-MD5 function
        /// </summary>
        /// <param name="strPassword">The user password</param>
        /// <returns>a human readable string</returns>
        public static String Crypt(String strPassword)
        {
            return Crypt(strPassword, GetRandomSalt());
        }

        /// <summary>
        /// Unix-like Crypt-MD5 function
        /// </summary>
        /// <param name="strPassword">The user password</param>
        /// <param name="strSalt">The salt or the pepper of the password</param>
        /// <returns>a human readable string</returns>
        public static string Crypt(string strPassword, string strSalt)
        {
            int iSaltEnd;
            int iLen;
            int iValue;
            int i;
            byte[] final;
            byte[] passwordBytes;
            byte[] saltBytes;
            byte[] ctx;

            StringBuilder sbResult;
            HashAlgorithm x_hash_alg = HashAlgorithm.Create("MD5");

            // Skip magic if it exists
            if (strSalt.StartsWith(_MAGIC))
                strSalt = strSalt.Substring(_MAGIC.Length);

            // Remove password hash if present
            if ((iSaltEnd = strSalt.LastIndexOf('$')) != -1)
                strSalt = strSalt.Substring(0, iSaltEnd);

            // Shorten salt to 8 characters if it is longer
            if (strSalt.Length > 8)
                strSalt = strSalt.Substring(0, 8);

            ctx = Encoding.ASCII.GetBytes((strPassword + _MAGIC + strSalt));
            final = x_hash_alg.ComputeHash(Encoding.ASCII.GetBytes((strPassword + strSalt + strPassword)));


            // Add as many characters of ctx1 to ctx
            for (iLen = strPassword.Length; iLen > 0; iLen -= 16)
            {
                if (iLen > 16)
                    ctx = _Concat(ctx, final);
                else
                    ctx = _PartialConcat(ctx, final, iLen);
            }

            // Then something really weird...
            passwordBytes = Encoding.ASCII.GetBytes(strPassword);

            for (i = strPassword.Length; i > 0; i >>= 1)
            {
                if ((i & 1) == 1)
                    ctx = _Concat(ctx, new byte[] { 0 });
                else
                    ctx = _Concat(ctx, new byte[] { passwordBytes[0] });
            }

            final = x_hash_alg.ComputeHash(ctx);

            byte[] ctx1;

            // Do additional mutations
            saltBytes = Encoding.ASCII.GetBytes(strSalt);//.getBytes();
            for (i = 0; i < 1000; i++)
            {
                ctx1 = new byte[] { };
                if ((i & 1) == 1)
                    ctx1 = _Concat(ctx1, passwordBytes);
                else
                    ctx1 = _Concat(ctx1, final);

                if (i % 3 != 0)
                    ctx1 = _Concat(ctx1, saltBytes);
                if (i % 7 != 0)
                    ctx1 = _Concat(ctx1, passwordBytes);
                if ((i & 1) != 0)
                    ctx1 = _Concat(ctx1, final);
                else
                    ctx1 = _Concat(ctx1, passwordBytes);

                final = x_hash_alg.ComputeHash(ctx1);

            }

            sbResult = new StringBuilder(64);

            sbResult.Append(_MAGIC);
            sbResult.Append(strSalt);
            sbResult.Append('$');

            // Add the password hash to the result string
            iValue = (final[0] << 16) | (final[6] << 8) | final[12];
            _ToBase64(sbResult, iValue, 4);
            iValue = (final[1] << 16) | (final[7] << 8) | final[13];
            _ToBase64(sbResult, iValue, 4);
            iValue = (final[2] << 16) | (final[8] << 8) | final[14];
            _ToBase64(sbResult, iValue, 4);
            iValue = (final[3] << 16) | (final[9] << 8) | final[15];
            _ToBase64(sbResult, iValue, 4);
            iValue = (final[4] << 16) | (final[10] << 8) | final[5];
            _ToBase64(sbResult, iValue, 4);
            iValue = final[11];
            _ToBase64(sbResult, iValue, 2);

            // Return result string
            return sbResult.ToString();
        }

    }
}
