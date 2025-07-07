using System.Collections.Generic;
using PX.SmsProvider;

namespace CelcomAfrica.SmsProvider
{
    public class CelcomAfricaSmsProviderFactory : ISmsProviderFactory
    {
        public ISmsProvider Create(IEnumerable<ISmsProviderSetting> settings)
        {
            var provider = new CelcomAfricaSmsProvider();
            provider.LoadSettings(settings);
            return provider;
        }

        public ISmsProvider Create()
        {
            return new CelcomAfricaSmsProvider();
        }

        public string Description => Messages.ProviderName;
        public string Name => typeof(CelcomAfricaSmsProvider).FullName;
    }
}