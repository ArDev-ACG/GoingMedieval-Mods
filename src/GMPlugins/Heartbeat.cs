using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// The clock the three sweeps hang off.
    ///
    /// <b>Why this exists.</b> The sweeps used to be coroutines started on the
    /// BepInEx plugin component itself. Three of them - the sun, the stake and
    /// the horde watchdog - between them wrote <em>not one line</em> across a
    /// full session that had a vampire walking around at noon, an impaled body
    /// standing at the gate and a horde stalled in a field. In the same log,
    /// the one thing that does the same work off a Harmony patch rather than a
    /// coroutine - <see cref="CountAura"/>, reading the same
    /// <c>WorkerManager.WorkersHere</c> - wrote a hundred and nine. Three
    /// silences and one witness is not a coincidence; whatever the reason, a
    /// coroutine hung on somebody else's component is not something this mod
    /// can keep betting on.
    ///
    /// So the clock is ours: one GameObject that this plugin creates, marks
    /// <c>DontDestroyOnLoad</c> and keeps a reference to, and one
    /// <c>Update()</c> that walks a list of jobs against
    /// <c>Time.unscaledTime</c>. No yield, no pooling, nothing that can be
    /// collected or reset behind our back, and it keeps running while the game
    /// is paused - which matters, because a horde standing still is exactly
    /// what a paused game looks like to a job that measures progress.
    ///
    /// <b>Every job says the first time it runs.</b> A sweep that finds nothing
    /// and a sweep that never ran read identically in a log, and telling those
    /// two apart cost most of a test cycle. One line per job at first tick, and
    /// the question is answered before it is asked next time.
    /// </summary>
    internal sealed class Heartbeat : MonoBehaviour
    {
        private sealed class Job
        {
            internal string Name;
            internal float Every;
            internal float Next;
            internal Action Work;
            internal bool Announced;
            internal int Failures;
            internal double SpentMs;
            internal double WorstMs;
        }

        /// <summary>Un tiron: un frame mas largo que esto se apunta con lo que corrio en el.</summary>
        private const float HitchSeconds = 0.15f;

        private const float ReportSeconds = 60f;

        private readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        private float reportAt;
        private int frames;
        private float frameTime;
        private float worstFrame;
        private int hitches;
        private string lastFrameJobs;

        private const int FailuresBeforeGivingUp = 10;

        private static Heartbeat live;

        private readonly List<Job> jobs = new List<Job>();
        private readonly List<Job> once = new List<Job>();

        /// <summary>
        /// The one object all of this runs on. Created on first use, kept
        /// across scene loads, and hidden from the hierarchy the same way
        /// BepInEx hides its own.
        /// </summary>
        internal static Heartbeat Live()
        {
            if (live != null) return live;

            try
            {
                var host = new GameObject("Aldrich_Heartbeat");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;

                live = host.AddComponent<Heartbeat>();
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogError($"[beat] could not start the clock: {e}");
            }

            return live;
        }

        internal void Repeat(string name, float seconds, Action work)
        {
            if (work == null || seconds <= 0f) return;

            // Desfasados: todos se daban de alta en el mismo frame, asi que los
            // de 0,5, 1, 1,5 y 3 s caian juntos cada tres segundos.
            var phase = (jobs.Count * 0.173f) % seconds;
            jobs.Add(new Job
            {
                Name = name,
                Every = seconds,
                Next = Time.unscaledTime + seconds + phase,
                Work = work,
            });
        }

        internal void After(float seconds, Action work)
        {
            if (work == null) return;

            once.Add(new Job
            {
                Name = "deferred",
                Every = seconds,
                Next = Time.unscaledTime + Mathf.Max(0f, seconds),
                Work = work,
                Announced = true,
            });
        }

        /// <summary>
        /// The clock, readable from any thread.
        ///
        /// <c>UnityEngine.Time.time</c> is main-thread only, and asking for it
        /// anywhere else does not hand back a stale number - it <b>throws</b>:
        /// <c>get_time can only be called from the main thread</c>. The GOAP
        /// runs the horde's target search on a worker thread
        /// (<c>ThreadingJobSystem</c> -&gt; <c>TargetBestWorkerThread</c>), so a
        /// patch on it that timestamps anything is one line away from killing
        /// the whole threaded task - which is exactly what happened: six of
        /// those in one session, each one a tick where the horde chose nobody,
        /// reported only as a <c>[WARN] [ThreadingJobSystem]</c> that says
        /// nothing about hordes.
        ///
        /// So the value is copied here once a frame, on the main thread, and
        /// everything off it reads this instead. A float write is atomic;
        /// `volatile` is only so a worker thread is not handed a value cached
        /// in a register from minutes ago.
        /// </summary>
        internal static volatile float Now;

        private void Update()
        {
            var now = Time.unscaledTime;
            Now = now;

#if !MINIMAP_STANDALONE
            Measure(now);
#endif

            for (var i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (now < job.Next) continue;

                job.Next = now + job.Every;
                Run(job);
            }

            for (var i = once.Count - 1; i >= 0; i--)
            {
                var job = once[i];
                if (now < job.Next) continue;

                once.RemoveAt(i);
                Run(job);
            }
        }

        /// <summary>
        /// Los FPS, en el log: el juego no escribe ni uno, y "va lento" sin un
        /// numero no se puede perseguir. Una linea por minuto con la media, el
        /// peor frame y los barridos nuestros que mas costaron; y cada tiron
        /// dice si en el frame anterior corrio algo nuestro, para separar lo
        /// del mod de lo del juego sin adivinar.
        /// </summary>
        private void Measure(float now)
        {
            var dt = Time.unscaledDeltaTime;
            frames++;
            frameTime += dt;
            if (dt > worstFrame) worstFrame = dt;

            if (dt >= HitchSeconds && dt < 10f)
            {
                hitches++;
                if (hitches <= 20)
                {
                    GMPlugin.Log?.LogInfo($"[fps] tiron de {dt * 1000f:F0} ms; nuestro en ese frame: "
                                          + (lastFrameJobs ?? "nada de 1 ms o mas"));
                }
            }

            lastFrameJobs = null;

            if (reportAt <= 0f) reportAt = now + ReportSeconds;
            if (now < reportAt) return;

            var top = new List<Job>(jobs);
            top.Sort((a, b) => b.SpentMs.CompareTo(a.SpentMs));
            var costly = new List<string>();
            for (var i = 0; i < top.Count && i < 4; i++)
            {
                if (top[i].SpentMs < 1.0) break;
                costly.Add($"{top[i].Name} {top[i].SpentMs:F0} ms (peor {top[i].WorstMs:F1})");
            }

            GMPlugin.Log?.LogInfo(
                $"[fps] ultimo minuto: {(frameTime > 0f ? frames / frameTime : 0f):F0} fps de media, "
                + $"peor frame {worstFrame * 1000f:F0} ms, {hitches} tiron(es) de {HitchSeconds * 1000f:F0} ms o mas; "
                + "barridos: " + (costly.Count == 0 ? "ninguno pasa de 1 ms" : string.Join(", ", costly.ToArray())));

            foreach (var job in jobs)
            {
                job.SpentMs = 0;
                job.WorstMs = 0;
            }

            frames = 0;
            frameTime = 0f;
            worstFrame = 0f;
            hitches = 0;
            reportAt = now + ReportSeconds;
        }

        /// <summary>
        /// Runs one job and keeps it honest. A job that throws ten times in a
        /// row is a job that will throw forever, and a stack trace once every
        /// four seconds for the rest of the session buries everything else in
        /// the log - so it is dropped, loudly, and the rest keep running.
        /// </summary>
        private void Run(Job job)
        {
            if (!job.Announced)
            {
                job.Announced = true;
                GMPlugin.Log?.LogInfo($"[beat] {job.Name} is running, every {job.Every}s");
            }

            try
            {
                watch.Restart();
                job.Work();
                watch.Stop();
                job.Failures = 0;

                var ms = watch.Elapsed.TotalMilliseconds;
                job.SpentMs += ms;
                if (ms > job.WorstMs) job.WorstMs = ms;
                if (ms >= 1.0) lastFrameJobs = (lastFrameJobs == null ? "" : lastFrameJobs + ", ") + $"{job.Name} {ms:F1} ms";
            }
            catch (Exception e)
            {
                job.Failures++;
                GMPlugin.Log?.LogError($"[beat] {job.Name} failed: {e}");

                if (job.Failures < FailuresBeforeGivingUp) return;

                jobs.Remove(job);
                GMPlugin.Log?.LogError(
                    $"[beat] {job.Name} failed {FailuresBeforeGivingUp} times running - dropped");
            }
        }
    }
}
