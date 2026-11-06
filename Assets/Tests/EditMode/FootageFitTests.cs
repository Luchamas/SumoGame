using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SumoGame;
using UnityEngine;

/// <summary>
/// Plays rival matches and compares their timing with values measured from footage of the
/// original fights, in Tools/FootageCheck/reference.json. Writes Logs/FootageCheck/fit.md.
/// </summary>
public class FootageFitTests
{
    const string ReferencePath = "Tools/FootageCheck/reference.json";
    const string ReportFolder = "Logs/FootageCheck";
    const float Dt = 0.02f;
    const int Fights = 60;
    const int MidRank = 5;

    [Serializable]
    class Metric
    {
        public string id, label, source, status, note;
        public float min, max, tolerance;
    }

    [Serializable]
    class Reference
    {
        public Metric[] metrics;
    }

    static Dictionary<string, float> Measure()
    {
        var rules = new SumoRules();
        var clinch = 0f;
        var clinches = 0;
        var rounds = 0;
        var roundTime = 0f;

        var seeds = new System.Random(21);
        for (var i = 0; i < Fights; i++)
        {
            var fight = new SumoSim();
            var a = new SumoBot(BotProfile.ForWins(MidRank), seeds.Next());
            var b = new SumoBot(BotProfile.ForWins(MidRank), seeds.Next());
            var holding = 0f;

            for (var t = 0f; t < 300f && fight.Phase != MatchPhase.MatchOver; t += Dt)
            {
                var wasFighting = fight.Phase == MatchPhase.Fighting;
                var time = fight.PhaseTime;
                fight.Step(Dt, a.Think(fight, 0, Dt), b.Think(fight, 1, Dt));

                if (wasFighting && fight.Phase == MatchPhase.RoundOver)
                {
                    rounds++;
                    roundTime += time;
                }

                var grabbing = fight.Fighters[0].IsLocked || fight.Fighters[0].State == FighterState.Driving
                    || fight.Fighters[1].State == FighterState.Driving;
                if (grabbing) holding += Dt;
                else if (holding > 0f)
                {
                    clinch += holding;
                    clinches++;
                    holding = 0f;
                }
            }
        }

        return new Dictionary<string, float>
        {
            ["clinch_seconds"] = clinch / Mathf.Max(1, clinches),
            ["round_seconds"] = roundTime / Mathf.Max(1, rounds),
            ["drive_seconds"] = rules.DriveTime,
            ["miss_seconds"] = rules.WhiffRecovery,
            ["ready_seconds"] = rules.CountdownTime,
            ["regrab_seconds"] = rules.RegrabDelay,
            ["fighting_distance"] = rules.EngageDistance,
            ["clash_seconds"] = ClashLength(),
            ["duck_seconds"] = DuckLength(),
        };
    }

    static SumoSim ReadyFight()
    {
        var fight = new SumoSim();
        while (fight.Phase != MatchPhase.Fighting) fight.Step(Dt, default, default);
        return fight;
    }

    /// <summary>Seconds from two grabs meeting until both can act again.</summary>
    static float ClashLength()
    {
        var fight = ReadyFight();
        var grab = new FighterInput { Grab = true };
        fight.Step(Dt, grab, grab);
        while (fight.Fighters[0].State == FighterState.GrabStartup) fight.Step(Dt, default, default);

        var time = 0f;
        for (; fight.Fighters[0].State == FighterState.Recoiling && time < 5f; time += Dt) fight.Step(Dt, default, default);
        return time;
    }

    /// <summary>Seconds from a strike starting until the grab that ducked it lands.</summary>
    static float DuckLength()
    {
        var fight = ReadyFight();
        fight.Step(Dt, new FighterInput { Grab = true }, default);
        fight.Step(Dt, default, new FighterInput { Strike = true });

        var time = 0f;
        for (; !fight.Fighters[0].IsLocked && time < 5f; time += Dt) fight.Step(Dt, default, default);
        return time;
    }

    [Test]
    public void GameplayStaysCloseToTheFootage()
    {
        var reference = JsonUtility.FromJson<Reference>(File.ReadAllText(ReferencePath));
        var measured = Measure();
        var drift = new List<string>();

        var report = new StringBuilder("| Measure | Footage | Game | Status |\n|---|---|---|---|\n");
        foreach (var metric in reference.metrics)
        {
            Assert.True(measured.ContainsKey(metric.id), $"nothing measures {metric.id}");

            var value = measured[metric.id];
            var fits = value >= metric.min - metric.tolerance && value <= metric.max + metric.tolerance;
            var state = fits ? "fits" : metric.status == "enforce" ? "DRIFT" : metric.status;
            if (state == "DRIFT") drift.Add($"{metric.label}: {value:F2}, footage {metric.min}-{metric.max}");
            report.Append($"| {metric.label} | {metric.min}-{metric.max} | {value:F2} | {state} |\n");
        }

        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText($"{ReportFolder}/fit.md", report.ToString());
        Debug.Log("FOOTAGE\n" + report);
        Assert.IsEmpty(drift, "Gameplay drifted from the footage:\n" + string.Join("\n", drift));
    }
}
