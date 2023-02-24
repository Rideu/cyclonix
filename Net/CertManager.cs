

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Cyclonix.Utils;

namespace Cyclonix.Net
{
    // Let's Encrypt certificate retrieval and usage
    //
    // Have OpenSSL installed, use "certbot certonly --standalone" cmd (additionally adding --key-type rsa).
    // After cert received, navigate and cmd cd to C:\Certbot\live\{mydomain}\ 
    // do "openssl pkcs12 -export -in fullchain.pem -inkey privkey.pem -out certp.pfx -certfile chain.pem"
    // copy "certp.pfx" to the server directory and pass it to the CertManager.LoadCert()
    // now you should have https working fine with Let's Encrypt

    // ALT: Use "certbot certonly --manual" cmd command if obtaining a cert with certbot,
    // make "http://{mydomain}/.well-known/acme-challenge/{hash}" open (as it will be said by the command),
    // follow cmd instructions, test by accessing that page, (probably, disable the firewall)
    // then proceed on cmd

    // Related to public key type (RSA/ECDSA): https://community.letsencrypt.org/t/certbot-issues-ecdsa-key-signed-with-rsa/189910

    public static class CertManager
    {
        static List<KeyValuePair<string, X509Certificate2>> certCollection = new List<KeyValuePair<string, X509Certificate2>>();
        static X509Certificate2 cert;

        public static List<KeyValuePair<string, X509Certificate2>> Certs => certCollection;

        public static X509Certificate Origin => cert;

        public static bool AddCert(string certpath, string? password)
        {

            var fullpath = certpath;

            if (File.Exists(fullpath))
            {

                var cert = new X509Certificate2(fullpath, password);

                var ctype = cert.PublicKey.Oid.FriendlyName;

                var key = cert.SubjectName.Name.Replace("CN=", "");
                certCollection.Add(new KeyValuePair<string, X509Certificate2>(key, cert));
                LogWriter.LogMsgTag("[I]", ($"{key} certificate added [PK type: {ctype}{(ctype == "RSA" ? "" : " (rather not supported)")}]. Valid till {cert.NotAfter:G}"), ConsoleColor.Green);

                var exp = (cert.NotAfter.Subtract(DateTime.Now));

                if (exp.TotalSeconds < 0)
                    LogWriter.LogMsgTag("[W]", ($"{key} certificate is EXPIRED. Renewing required. SSL of the domain will be dropped"), ConsoleColor.Red);
                else
                if (exp.TotalDays < 7)
                    LogWriter.LogMsgTag("[W]", ($"{key} certificate is about to be expired soon ({(int)exp.TotalDays} days left). Renewing required"), ConsoleColor.Yellow);
                return true;
            }
            else
            {

                LogWriter.LogMsgTag("[I]", ($"Certificate not found at {fullpath}"), ConsoleColor.Red);
            }
            return false;
        }

        public static X509Certificate2 GetCert(string hostname)
        {

            if (hostname == null) return null;

            for (int i = 0; i < certCollection.Count; i++)
            {

                var key = certCollection[i].Key;

                if (hostname == key)
                    return certCollection[i].Value;

                var iswildcard = key.StartsWith("*.");

                if (iswildcard)
                {
                    if (hostname.EndsWith(key.Substring(2)))
                    {
                        return certCollection[i].Value;
                    }
                }
            }
            return null;
        }

        public static bool LoadCert(string certpath, string? certpass)
        {

            var fullpath = Environment.CurrentDirectory + (certpath);
            if (File.Exists(fullpath) && cert == null)
            {

                cert = new X509Certificate2(fullpath, certpass);

                LogWriter.LogMsgTag("[I]", ("Certificate found"), ConsoleColor.Green);
                return true;
            }
            return false;
        }

        public static bool LoadCertFolder(string folderPath)
        {

            var certpemPath = folderPath + "\\cert.pem";
            var pkpemPath = folderPath + "\\privkey.pem";

            if (Directory.Exists(folderPath) && cert == null)
            {

                //cert = new X509Certificate2(certPath, string.Empty);
                var certPem = File.ReadAllText(certpemPath);
                var eccPem = File.ReadAllText(pkpemPath);

                cert = X509Certificate2.CreateFromPem(certPem, eccPem);
                //File.WriteAllBytes(folderPath + "\\certp.pfx", cert.Export(X509ContentType.Pfx));
                LogWriter.LogMsgTag("[I]", ("Certificate found"), ConsoleColor.Cyan);
                return true;
            }
            return false;
        }

        //public static bool CreateServerKeys(string domainName, string folderPath)
        //{
        //    var certpemPath = folderPath + "\\cert.pem";
        //    var pkpemPath = folderPath + "\\privkey.pem";
        //    var certPath = folderPath + "\\certp.p12";
        //    var authPath = folderPath + "\\auth.pem";

        //    //if (new DirectoryInfo(folderPath).Exists && File.Exists(certPath))
        //    ////if (File.Exists("cert.pem"))
        //    //{
        //    //    cert = new X509Certificate2(certPath, (string)null);
        //    //    //bool valid = cert.Verify();
        //    //    //return valid;
        //    //    return true;
        //    //}
        //    //else
        //    {

        //        //cert.Export(X509ContentType.Pkcs12, "password");

        //        var certPem = File.ReadAllText(certpemPath);
        //        var eccPem = File.ReadAllText(pkpemPath);

        //        var cert = X509Certificate2.CreateFromPem(certPem, eccPem);
        //        File.WriteAllBytes(folderPath + "\\certp.pfx", cert.Export(X509ContentType.Pfx));

        //        var rsaKey = RSA.Create(2048);
        //        string subject = "CN=" + domainName;
        //        var certReq = new CertificateRequest(subject, rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        //        certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        //        certReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));
        //        var expirate = DateTimeOffset.Now.AddDays(90);

        //        var caCert = certReq.CreateSelfSigned(DateTimeOffset.Now, expirate);
        //        //var capem = GetCert(authPath).UTF8Decode();
        //        //var cawdwCert = X509Certificate2.GetCertContentType(certpemPath);
        //        //var ceccrt = X509Certificate2.CreateFromEncryptedPemFile(authPath, pkpemPath);
        //        //var caCert = X509Certificate2.CreateFromEncryptedPemFile(certpemPath, string.Empty, pkpemPath);

        //        var clientKey = RSA.Create(2048);
        //        var clientReq = new CertificateRequest(subject, clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        //        clientReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        //        clientReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));
        //        clientReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(clientReq.PublicKey, false));

        //        byte[] serialNumber = BitConverter.GetBytes(DateTime.Now.ToBinary());

        //        var clientCert = clientReq.Create(caCert, DateTimeOffset.Now, expirate, serialNumber);

        //        var exportCert = new X509Certificate2(clientCert.Export(X509ContentType.Cert), (string)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet).CopyWithPrivateKey(clientKey);
        //        //File.WriteAllBytes("client.pfx", exportCert.Export(X509ContentType.Pfx));
        //        File.WriteAllBytes(folderPath + "\\cert.p12", exportCert.Export(X509ContentType.Pkcs12));
        //        cert = exportCert;
        //        //cert.Verify();
        //        return true;
        //    }
        //}
    }
}
