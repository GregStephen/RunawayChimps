using System;
using RunawayChimps.Tests.KnockBack;

internal static class Program
{
    private static int Main()
    {
        try
        {
            foreach (string name in VentModelChecks.Cases)
            {
                VentModelChecks.Run(name);
                Console.WriteLine("PASS: " + name);
            }
            Console.WriteLine($"PASS: {VentModelChecks.Cases.Length} managed cases / {VentModelChecks.Assertions} assertions against production core. Not Unity, Photon or headset execution.");
            return 0;
        }
        catch (Exception failure) { Console.Error.WriteLine(failure); return 1; }
    }
}
