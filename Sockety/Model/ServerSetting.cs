using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Sockety.Model
{
    public class ServerSetting
    {
        public bool UseSSL { get; set; }
        public X509Certificate Certificate { get; private set; }

        public void LoadCertificateFile(string CertificateFileName,string Password)
        {
            Certificate = new X509Certificate("test-cert.pfx", "testcert");
        }
    }
}
