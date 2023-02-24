using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Cyclonix.Net
{
    public partial class NetServer
    {


        SslServerAuthenticationOptions serverAuthOptions;

        // How to enable TLS 1.3 
        // https://learn.microsoft.com/en-us/windows-server/security/tls/tls-registry-settings?tabs=diffie-hellman#tls-dtls-and-ssl-protocol-version-settings
        public SslServerAuthenticationOptions SSLOptions => serverAuthOptions;

        public void SetupSSL(params SslApplicationProtocol[] applicationProtocols)
        {

            serverAuthOptions = new()
            { 
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                AllowRenegotiation = false,
                ClientCertificateRequired = false,
                CertificateRevocationCheckMode = X509RevocationMode.Offline,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                RemoteCertificateValidationCallback = remoteCertCallback,
                ServerCertificateSelectionCallback = certCallback,
                ApplicationProtocols = new()
            };


            if (applicationProtocols?.Length > 0)
            {
                serverAuthOptions.ApplicationProtocols.AddRange(applicationProtocols);
            }
        }

        bool remoteCertCallback(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslPolicy)
        {

            if (sslPolicy == SslPolicyErrors.RemoteCertificateNotAvailable)
                return true;
            return false;
        }

        X509Certificate2 certCallback(object sender, string asd)
        {

            if (sender is SslStream sslstream)
            {

                var host = sslstream.TargetHostName;
                var cert = CertManager.GetCert(host);
                return cert;
            }
            else
            if (sender is TLSStream tlsstream)
            {

                var host = tlsstream.TargetHostName;
                var cert = CertManager.GetCert(host);
                return cert;
            }
            return null;
        }
    }
}
