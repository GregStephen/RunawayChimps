using System;
using RunawayChimps.Toys;

// Executes the shipped pure policy, not a Python port or a second implementation.
internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(name);
    }
    private static void Step(SpecimenJarState s, float t, int hand = -1, bool seen = false,
        bool away = false, float chance = 1f, float roll = 0f)
        => s.Step(t, hand, seen, away, 0.8f, 18f, 4f, chance, roll);
    private static void Arm(SpecimenTapGate gate)
    {
        for (int i = 0; i < 8; ++i)
            Check(!gate.Sample(true, false, true, 0.02f, 0f, 0.3f, 0.12f), "Release is not a tap");
    }
    private static void Main()
    {
        Check(SpecimenJarState.Nearest(.2f,.4f,.55f) == 0, "Left nearest");
        Check(SpecimenJarState.Nearest(.4f,.2f,.55f) == 1, "Right nearest");
        Check(SpecimenJarState.Nearest(.2f,.2f,.55f) == 0, "Deterministic tie");
        Check(SpecimenJarState.Nearest(.55f,1f,.55f) == 0, "Inclusive radius");
        Check(SpecimenJarState.Nearest(.56f,1f,.55f) == -1, "Out of range");
        Check(SpecimenJarState.Nearest(float.NaN,.2f,.55f) == 1, "Missing left");
        Check(SpecimenJarState.Nearest(.2f,float.PositiveInfinity,.55f) == 0, "Missing right");
        Check(SpecimenJarState.Nearest(float.NaN,float.PositiveInfinity,.55f) == -1, "No eligible hands");
        Check(SpecimenJarState.Nearest(-.2f,-1f,.55f) == -1, "Invalid negative distances");
        var rng = new Random(431);
        for (int i = 0; i < 5000; i++)
        {
            float x=(float)rng.NextDouble()*20-10, y=(float)rng.NextDouble()*20-10, z=(float)rng.NextDouble()*20-10;
            float a=SpecimenJarState.BoundsScale(x,y,z,.095f,.14f,.095f);
            double q=x*a/.095, r=y*a/.14, t=z*a/.095;
            Check(q*q+r*r+t*t <= 1.000001, "Bounds contain target");
            Check(a > 0 && a <= 1, "Bounds never expand");
        }
        Check(SpecimenJarState.BoundsScale(0,0,0,.1f,.2f,.1f)==1, "Center remains finite");
        Check(SpecimenJarState.BoundsScale(.01f,.01f,.01f,.1f,.2f,.1f)==1, "Interior unchanged");
        Check(SpecimenJarState.BoundsScale(float.NaN,0,0,.1f,.2f,.1f)==0, "NaN bounds input");
        Check(SpecimenJarState.BoundsScale(1,1,1,0,.2f,.1f)==0, "Zero extent fails safe");
        Check(SpecimenJarState.BoundsScale(float.MaxValue,1,1,.1f,.2f,.1f)>0, "Huge finite point is stable");

        var gate = new SpecimenTapGate();
        Check(!gate.Sample(false,true,false,.02f,2f,.3f,.12f), "Initial overlap is not a tap");
        Arm(gate);
        Check(gate.Sample(true,true,false,.02f,.31f,.3f,.12f), "Genuine tap accepted");
        for(int i=0;i<100;i++) Check(!gate.Sample(true,true,false,.02f,2f,.3f,.12f), "Rest/duplicate chatter rejected");
        Arm(gate);
        Check(!gate.Sample(true,true,false,.02f,.1f,.3f,.12f), "Slow entry rejected");
        Check(!gate.Sample(true,true,false,.02f,4f,.3f,.12f), "Acceleration while resting rejected");
        Arm(gate);
        Check(!gate.Sample(false,true,false,.02f,4f,.3f,.12f), "Tracking discontinuity rejected");
        Check(!gate.Sample(true,true,false,.02f,4f,.3f,.12f), "No tap immediately after reacquisition");
        Arm(gate); gate.Reset();
        Check(!gate.Sample(true,true,false,.02f,4f,.3f,.12f), "Disable/travel reset disarms");
        Arm(gate);
        Check(!gate.Sample(true,true,false,.2f,4f,.3f,.12f), "Long frame is not a tap");
        Arm(gate);
        Check(!gate.Sample(true,true,false,.02f,float.NaN,.3f,.12f), "Invalid velocity rejected");
        for (int i=0;i<5;i++) gate.Sample(true,false,true,.02f,0,.3f,.12f);
        gate.Sample(true,false,false,.02f,0,.3f,.12f);
        Check(!gate.Sample(true,true,false,.02f,4,.3f,.12f), "Hysteresis needs sustained full withdrawal");
        Arm(gate);
        Check(gate.Sample(true,true,false,.02f,4,.3f,.12f), "Fresh tap after release");

        var s=new SpecimenJarState(); s.Reset(0,18);
        Step(s,1,0,true); Check(s.Mood==SpecimenMood.Interested && s.Hand==0, "Interest left");
        Step(s,2,1,true); Check(s.Hand==1, "Switch right");
        Check(s.TryTap(2,.55f,.4f), "First recoil");
        Check(!s.TryTap(2,.55f,.4f), "Simultaneous/duplicate reaction");
        Step(s,2.2f,0,true); Check(s.Mood==SpecimenMood.Recoiling && s.Hand==-1, "Recoil priority");
        Check(!s.TryTap(2.5f,.55f,.4f), "Global cooldown");
        Step(s,2.6f,1,true); Check(s.Mood==SpecimenMood.Interested, "Resume curiosity");
        Check(s.TryTap(2.6f,.55f,.4f), "Later tap");
        s.Reset(3,18); Check(s.Mood==SpecimenMood.Idle && s.Hand==-1 && s.ReactionCount==0 && s.WatchCount==0, "Complete reset");
        Step(s,4,-1,true); Step(s,5,-1,false,true); Step(s,6,-1,false,true);
        Check(s.WatchCount==0, "Initial watch cooldown");
        Step(s,22,-1,false,true); Check(s.Mood==SpecimenMood.Watching && s.WatchCount==1, "Seen then away triggers watch");
        Step(s,22.1f,-1,true); Check(s.Mood==SpecimenMood.Watching, "Watch persists on return");
        Step(s,23,-1,false,true); Step(s,24,-1,false,true); Check(s.WatchCount==1, "Return/away cannot bypass cooldown");
        Step(s,50,-1,true,true); Check(s.WatchCount==1, "Clearly watching wins conflicting visibility inputs");
        s.Reset(0,1); Step(s,2,-1,false,true); Step(s,3,-1,false,true);
        Check(s.WatchCount==0, "Must have been seen first");
        Step(s,4,-1,true); Step(s,5,-1,false,true); Step(s,6,-1,false,true,0,0.2f);
        Check(s.WatchCount==0, "Chance zero");
        Step(s,50,-1,false,true); Check(s.WatchCount==0, "One roll per away episode");
        Step(s,51,-1,true); Step(s,52,-1,false,true); Step(s,53,-1,false,true);
        Check(s.WatchCount==1, "New look-away can roll");
        Step(s,53.1f,0,false,true); Check(s.Mood==SpecimenMood.Interested, "Hand interrupts watch");
        Step(s,53.2f); Check(s.Mood==SpecimenMood.Idle, "No stale watch resumes");
        s.Reset(54,18); Step(s,100,-1,false,true); Step(s,101,-1,false,true);
        Check(s.Mood==SpecimenMood.Idle && s.WatchCount==0, "Reset clears gaze history");
        Console.WriteLine($"PASS: {assertions} production specimen-policy assertions (including 5000 bounds samples).");
        Console.WriteLine("Pure managed policy only. Unity import, real tracking, native collider queries, rendering and Photon/headset acceptance are separate.");
    }
}
