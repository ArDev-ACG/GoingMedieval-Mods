using System;
using System.Collections.Generic;
using NSMedieval;
using NSMedieval.Enums;
using NSMedieval.Manager;
using NSMedieval.State;
using NSMedieval.Village.Map;
using UnityEngine;

namespace Aldrich.GMPlugins
{
    /// <summary>
    /// El Corredor sube por las paredes.
    ///
    /// <b>Por que no es "trepar" de verdad.</b> El movimiento del juego va de
    /// nodo en nodo y una pared no tiene nodos: el buscador de caminos no sabe
    /// que haya otra forma de pasar. Asi que no se le ensena a trepar, se le
    /// pasa: el cuerpo (el lobo invisible) salta al sitio de llegada y espera
    /// quieto, y el modelo recorre la pared con <see cref="XenoBody.Climb"/>.
    ///
    /// <b>Cuando.</b> Un Corredor con un colono a menos de <see cref="Sense"/>
    /// casillas que lleva <see cref="StuckSeconds"/> sin acercarsele, y con
    /// pared delante en esa direccion. No mira si habia puerta: un bicho que
    /// trepa no busca la puerta.
    ///
    /// <b>Torres.</b> Sube la columna de la pared nivel a nivel hasta donde se
    /// acaba - hasta <c>Runner.ClimbLevels</c> -, asi que una torre de tres
    /// pisos es una subida mas larga, no un caso aparte. Arriba hay dos sitios
    /// donde quedarse: encima de la pared, si alli se puede estar (un adarve,
    /// un suelo sobre el muro), o al otro lado, cayendo hasta el primer suelo
    /// - el patio, o el piso alto de la torre -. Se queda con el que deje mas
    /// cerca del colono, contando la altura.
    /// </summary>
    internal static class XenoClimb
    {
        private const float SweepSeconds = 1f;
        private const float Sense = 40f;
        private const float StuckSeconds = 4f;
        private const float Cooldown = 6f;
        private const float Close = 2.5f;

        /// <summary>Lo que cuenta como pared. Las puertas no: esas se rompen.</summary>
        private const BuildingType Climbable =
            BuildingType.Wall | BuildingType.Window | BuildingType.Fence
            | BuildingType.FenceGate | BuildingType.Merlon | BuildingType.Beam;

        private static readonly int[,] Sides = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        private sealed class Track
        {
            internal float Best = float.MaxValue;
            internal float Since;
            internal float Quiet;
        }

        private struct Route
        {
            internal Vector3 From, Top, To, Face;
            internal float Score;
        }

        private static readonly Dictionary<int, Track> Tracks = new Dictionary<int, Track>();
        private static bool said;
        private static bool saidStuck;

        internal static void Start()
        {
            GMPlugin.Every("xeno climb", SweepSeconds, Sweep);
        }

        private static void Sweep()
        {
            if (!(GMPlugin.XenoClimbEnabled?.Value ?? true)) return;
            if (!NSEipix.Base.MonoSingleton<WorkerManager>.IsInstantiated()) return;

            var map = NSMedieval.Village.VillageManager.ActiveVillage?.Map;
            if (map == null) return;

            var now = Time.time;
            var seen = new HashSet<int>();

            foreach (var body in Census.Everything())
            {
                var runner = body as AnimalInstance;
                if (runner == null || runner.HasDisposed || runner.HasDied) continue;
                if (runner.Id != XenoRunnerModel.AnimalId && runner.Blueprint?.GetID() != XenoRunnerModel.AnimalId) continue;

                seen.Add(runner.UniqueId);
                try
                {
                    Watch(map, runner, now);
                }
                catch (Exception e)
                {
                    GMPlugin.Log?.LogError($"[climb] {GameAccess.Name(runner)}#{runner.UniqueId}: {e}");
                }
            }

            if (Tracks.Count > seen.Count)
            {
                var gone = new List<int>();
                foreach (var id in Tracks.Keys) if (!seen.Contains(id)) gone.Add(id);
                foreach (var id in gone) Tracks.Remove(id);
            }
        }

        private static void Watch(VillageMap map, AnimalInstance runner, float now)
        {
            Track track;
            if (!Tracks.TryGetValue(runner.UniqueId, out track))
            {
                track = new Track { Since = now };
                Tracks[runner.UniqueId] = track;
            }

            var view = runner.GetAgentView<NSMedieval.View.AnimatedAgentView>();
            var body = view != null ? view.GetComponent<XenoBody>() : null;
            if (body == null || body.Climbing || now < track.Quiet) return;

            var prey = Nearest(runner);
            if (prey == null) { Reset(track, now); return; }

            var gap = Flat(runner.GetPosition(), prey.GetPosition());
            if (gap > Sense || gap <= Close) { Reset(track, now); return; }

            // Se esta acercando: que siga su camino.
            if (gap < track.Best - 0.5f)
            {
                track.Best = gap;
                track.Since = now;
                return;
            }

            if (now - track.Since < StuckSeconds) return;

            Route route;
            if (!Plan(map, runner.GetGridPosition(), prey.GetPosition(), out route))
            {
                if (!saidStuck)
                {
                    saidStuck = true;
                    GMPlugin.Log?.LogInfo(
                        $"[climb] {GameAccess.Name(runner)}#{runner.UniqueId} no se acerca a {GameAccess.Name(prey)} "
                        + $"({gap:0} casillas) y no tiene pared delante que trepar (solo se avisa la primera)");
                }
                Reset(track, now);
                return;
            }

            var seconds = body.Climb(route.From, route.Top, route.To, route.Face);
            Hold(runner, route.To);
            GMPlugin.RunAfter(seconds, () => Release(runner));

            track.Quiet = now + seconds + Cooldown;
            Reset(track, now);

            if (!said)
            {
                said = true;
                GMPlugin.Log?.LogInfo(
                    $"[climb] {GameAccess.Name(runner)}#{runner.UniqueId} sube por la pared: "
                    + $"{route.Top.y - route.From.y:0.0} m arriba, {route.Top.y - route.To.y:0.0} m de caida "
                    + "(solo se avisa la primera)");
            }
        }

        private static void Reset(Track track, float now)
        {
            track.Best = float.MaxValue;
            track.Since = now;
        }

        /// <summary>
        /// La subida en nodos: un lado con pared hacia el colono; la columna de
        /// esa pared hacia arriba hasta que deja de ser pared; y donde
        /// quedarse, encima o al otro lado.
        /// </summary>
        private static bool Plan(VillageMap map, Vec3Int at, Vector3 goal, out Route route)
        {
            route = new Route { Score = float.MaxValue };

            var here = map.GetNode(at.x, at.y, at.z);
            if (here == null) return false;

            var toward = goal - here.WorldPosition;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f) return false;
            toward.Normalize();

            var levels = Mathf.Max(1, GMPlugin.XenoClimbLevels?.Value ?? 12);
            var found = false;

            for (var s = 0; s < 4; s++)
            {
                var sx = Sides[s, 0];
                var sz = Sides[s, 1];
                var dir = new Vector3(sx, 0f, sz);
                if (Vector3.Dot(dir, toward) < 0.3f) continue;

                var wx = at.x + sx;
                var wz = at.z + sz;
                if (!Wall(map.GetNode(wx, at.y, wz))) continue;

                // Hasta donde sube la pared.
                var k = 1;
                while (k <= levels && Wall(map.GetNode(wx, at.y + k, wz))) k++;
                if (k > levels) continue;

                var crest = map.GetNode(wx, at.y + k, wz);
                if (crest == null) continue;

                // Encima de la pared, si alli se puede estar.
                if (crest.IsWalkable) found |= Consider(here, crest, crest, goal, dir, ref route);

                // Al otro lado: uno o dos de grosor, y cayendo al primer suelo.
                for (var thick = 1; thick <= 2; thick++)
                {
                    var bx = wx + sx * thick;
                    var bz = wz + sz * thick;
                    if (Wall(map.GetNode(bx, at.y + k - 1, bz))) continue;

                    for (var y = at.y + k; y >= Mathf.Max(0, at.y - levels); y--)
                    {
                        var land = map.GetNode(bx, y, bz);
                        if (land == null) continue;
                        if (Wall(land)) break;
                        if (!land.IsWalkable) continue;

                        found |= Consider(here, crest, land, goal, dir, ref route);
                        break;
                    }
                    break;
                }
            }

            return found;
        }

        private static bool Consider(MapNode here, MapNode crest, MapNode land, Vector3 goal, Vector3 dir, ref Route route)
        {
            var score = Vector3.Distance(land.WorldPosition, goal);
            if (score >= route.Score) return false;

            route = new Route
            {
                From = here.WorldPosition,
                Top = crest.WorldPosition,
                To = land.WorldPosition,
                Face = dir,
                Score = score,
            };
            return true;
        }

        private static bool Wall(MapNode node)
        {
            return node != null && !node.IsWalkable && (node.BuildingType & Climbable) != 0;
        }

        private static HumanoidInstance Nearest(CreatureBase runner)
        {
            var here = runner.GetPosition();
            HumanoidInstance best = null;
            var gap = float.MaxValue;

            foreach (var settler in WorkerManager.WorkersHere)
            {
                if (settler == null || settler.HasDisposed || settler.HasDied) continue;

                var d = Flat(here, settler.GetPosition());
                if (d < gap) { gap = d; best = settler; }
            }

            return best;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        /// <summary>El cuerpo al sitio de llegada, y quieto mientras el modelo sube.</summary>
        private static void Hold(CreatureBase runner, Vector3 to)
        {
            try
            {
                runner.GetGoapAgent()?.StopTicker();
                runner.CombatAi?.StopTicker();
                runner.PathDriver?.Abort();
                runner.PathDriver?.Teleport(to);
            }
            catch (Exception e)
            {
                GMPlugin.Log?.LogWarning($"[climb] could not hold {GameAccess.Name(runner)}: {e.Message}");
            }
        }

        private static void Release(CreatureBase runner)
        {
            if (runner == null || runner.HasDisposed || runner.HasDied) return;

            try
            {
                runner.GetGoapAgent()?.StartTicker();
                runner.CombatAi?.StartTicker();
            }
            catch (Exception)
            {
                // Uno que el juego esta quitando.
            }
        }
    }
}
