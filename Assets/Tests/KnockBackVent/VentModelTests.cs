using NUnit.Framework;

namespace RunawayChimps.Tests.KnockBack
{
    public sealed class VentModelTests
    {
        [TestCaseSource(typeof(VentModelChecks), nameof(VentModelChecks.Cases))]
        public void ProductionModelContracts(string name) => VentModelChecks.Run(name);
    }
}
