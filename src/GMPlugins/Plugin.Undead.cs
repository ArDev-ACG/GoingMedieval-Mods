using HarmonyLib;

namespace Aldrich.GMPlugins
{
    /// <summary>Lo que el codigo de Undead Horde necesita del plugin: su config, sus parches y sus barridos.</summary>
    public partial class GMPlugin
    {
        internal static BepInEx.Configuration.ConfigEntry<float> HordeRaidBoost;

        internal static BepInEx.Configuration.ConfigEntry<int> UndeadMaxPerPrey;
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadBrawl;
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadSight;
        internal static BepInEx.Configuration.ConfigEntry<float> UndeadSightRange;
        /// <summary>
        /// Whether the horde, with nobody left to chase, turns on the buildings
        /// instead of the handful of things a vanilla raid is allowed to break.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadRazeEverything;
        /// <summary>
        /// Whether the horde attacks anything alive that is not one of its own,
        /// rather than walking past livestock and other factions to reach a
        /// settler the way a raid with a grievance would.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadAttackEverything;
        /// <summary>The mirror of it: whether the living all hate the dead.</summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadHatedByAll;
        internal static BepInEx.Configuration.ConfigEntry<bool> RisenNeverRetreats;
        /// <summary>
        /// Casillas de radio con las que un alzado caza: todo lo vivo que entre
        /// ahi es objetivo suyo. Es el numero de la regla, y bajarlo devuelve la
        /// horda al alcance corto del sensor del juego.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<int> UndeadHuntRadius;
        /// <summary>Si un alzado sin nada que hacer se pone a andar.</summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadRoam;
        /// <summary>Si los alzados acuden a donde otro esta peleando.</summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadAlert;
        /// <summary>Cuanto dura un aviso, en segundos reales.</summary>
        internal static BepInEx.Configuration.ConfigEntry<float> UndeadAlertSeconds;
        /// <summary>Hasta cuantas casillas se oye un aviso.</summary>
        internal static BepInEx.Configuration.ConfigEntry<int> UndeadAlertReach;
        /// <summary>
        /// Whether the horde has to fail at reaching a settler before it starts
        /// pulling the settlement apart. Off restores the old behaviour, where
        /// the raze goal outranked the chase because that is the order the
        /// game's own goal list happens to be in.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadChaseFirst;
        /// <summary>
        /// How long a failed chase keeps a walker in demolition mode, in
        /// seconds. Long enough that it does not drop the wall it is halfway
        /// through every time the search is retried; short enough that an
        /// opened door is noticed.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> RazeMemorySeconds;
        /// <summary>
        /// Whether a settler killed by the undead gets back up as one of them
        /// instead of leaving a body.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> RiseFromUndeadKill;
        /// <summary>
        /// The fraction of maximum health a settler gets back up with. Low
        /// enough that what stands up is easy to put down again, high enough
        /// that it does not fall over on the next tick.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> RisePercent;
        /// <summary>
        /// Whether a settler who dies while carrying the horde's infection gets
        /// back up, whatever it was that finally killed them.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> RiseFromInfectionDeath;
        /// <summary>
        /// The three numbers behind the fever. Real seconds between two bites,
        /// the size of one bite as a fraction of maximum health, and the share
        /// of maximum health the infection will never take a settler below -
        /// which is what keeps it a wound and not a countdown.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> InfectionDamageSeconds;
        internal static BepInEx.Configuration.ConfigEntry<float> InfectionDamagePercent;
        internal static BepInEx.Configuration.ConfigEntry<float> InfectionDamageFloor;
        /// <summary>
        /// Whether a walker that has been standing still long enough gets told
        /// to look for something to do. Off leaves a horde with nothing left to
        /// chase exactly where it stopped.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<bool> UndeadRestless;
        /// <summary>
        /// How long a walker may go without covering ground or landing a blow
        /// before it drops its target, its order and its goal and starts over.
        /// </summary>
        internal static BepInEx.Configuration.ConfigEntry<float> UndeadIdleSeconds;

        private void BindUndead()
        {
            HordeRaidBoost = Config.Bind("Undead", "HordeRaidBoost", 0.35f,
                "Chance that a raid picked for another hostile faction is brought by "
                + "the Horde instead, when the Horde is on the world map. 0 leaves the "
                + "game's even pick; with four hostile factions, 0.35 makes about half "
                + "the raids the Horde's.");

            // Decomposition, not dust. A body goes pallid within hours and then
            // green over the next few days, as the gut bacteria turn its
            // haemoglobin to sulfhaemoglobin - which is where the whole
            // convention of green-grey zombie skin comes from, and where the
            // Walking Dead walkers and Romero's blue-grey ghouls both sit.
            //
            // #8C9A8E was on that axis but too pale and too clean: under this
            // game's strong sun it read as painted clay. Darker and a shade
            // greener puts it clearly below every living skin tone and away
            // from the browns and ochres everything else here is made of.
            //
            // Other points on the same axis, for anyone who wants a different
            // decade of corpse: #9AA3A8 (Romero blue-grey, freshly dead),
            // #B9AEB0 (livor mortis, pallid with a violet cast), #6A7358
            // (well past ripe).
            RisenNeverRetreats = Config.Bind("Undead", "RisenNeverRetreats", true,
                "Un alzado no abandona el mapa aunque su incursion se de por "
                + "terminada, ni aunque pierda de vista el raid al que "
                + "pertenecia - que es la via por la que se iban solos. "
                + "Rendirse ya lo cubria la faccion; irse andando no.");

            UndeadAttackEverything = Config.Bind("Undead", "AttackEverything", true,
                "The horde treats anything alive that is not one of its own as "
                + "a target - livestock, traders, other factions' raiders - "
                + "instead of walking past everything to reach a settler.");

            UndeadHatedByAll = Config.Bind("Undead", "HatedByAll", true,
                "Anything alive treats one of the dead as an enemy on sight - "
                + "raiders, traders' guards, wildlife - instead of leaving it "
                + "alone because its faction is not at war with theirs.");

            UndeadRazeEverything = Config.Bind("Undead", "RazeEverything", true,
                "With nobody left to chase, let the horde tear down anything "
                + "the player built instead of only the few building types a "
                + "vanilla raid is allowed to spread out over.");

            UndeadHuntRadius = Config.Bind("Undead", "HuntRadius", 25,
                "How far a walker notices anything alive, in tiles. Everything "
                + "inside is a target, settlers first on a tie.");

            UndeadMaxPerPrey = Config.Bind("Undead", "MaxPerPrey", 4,
                "How many walkers go for the same creature at once. Melee needs "
                + "a free tile next to the target and there are eight at most; "
                + "the rest pick the next closest thing, or wait nearby.");

            UndeadBrawl = Config.Bind("Undead", "Brawl", true,
                "When a walker and a living non-settler stand toe to toe and the "
                + "game's melee finds no free tile to attack from, they trade "
                + "blows anyway through the game's own melee call. See RisenBrawl.");

            UndeadSight = Config.Bind("Undead", "Sight", true,
                "A walker and a living non-settler go for each other as soon as "
                + "they see each other, instead of waiting for the first blow. "
                + "See RisenSighting.");

            UndeadSightRange = Config.Bind("Undead", "SightRange", 14f,
                "How far that is, in tiles.");

            UndeadRoam = Config.Bind("Undead", "Roam", true,
                "A walker with nothing in reach walks towards the village "
                + "instead of standing still, so its radius travels with it.");

            UndeadAlert = Config.Bind("Undead", "Alert", true,
                "When one walker attacks, the others learn where it is standing "
                + "and head there.");

            UndeadAlertSeconds = Config.Bind("Undead", "AlertSeconds", 45f,
                "How long one of those alerts is worth walking to.");

            UndeadAlertReach = Config.Bind("Undead", "AlertReach", 60,
                "How far an alert carries, in tiles. Wider than HuntRadius on "
                + "purpose: it is for the walkers that cannot see anything.");

            UndeadChaseFirst = Config.Bind("Undead", "ChaseFirst", true,
                "The horde only starts breaking things once it has failed to "
                + "find a way to a settler. Off lets the raze goal run first, "
                + "which is the order the game's own goal list is in.");

            RazeMemorySeconds = Config.Bind("Undead", "RazeMemorySeconds", 20f,
                "How long a failed chase keeps a walker breaking things before "
                + "it looks for a way in again.");

            RiseFromUndeadKill = Config.Bind("Undead", "RiseFromUndeadKill", true,
                "A settler killed by the undead gets back up as one of them "
                + "instead of leaving a body.");

            RisePercent = Config.Bind("Undead", "RisePercent", 0.25f,
                "Fraction of maximum health a settler gets back up with after "
                + "being killed by the undead.");

            RiseFromInfectionDeath = Config.Bind("Undead", "RiseFromInfectionDeath", true,
                "A settler who dies while carrying the festering wound gets "
                + "back up as one of them, whatever it was that killed them. "
                + "The one-in-three roll is the chance of surviving the wound; "
                + "somebody who died of it did not.");

            InfectionDamageSeconds = Config.Bind("Undead", "InfectionDamageSeconds", 14f,
                "Real seconds between two bites of the infection while a "
                + "settler is carrying it. 0 leaves the wound to its JSON "
                + "penalties alone.");

            InfectionDamagePercent = Config.Bind("Undead", "InfectionDamagePercent", 0.03f,
                "How much of maximum health one bite of the infection takes.");

            InfectionDamageFloor = Config.Bind("Undead", "InfectionDamageFloor", 0.25f,
                "Share of maximum health the infection will never take a "
                + "settler below. It is meant to hurt, not to kill: what "
                + "finishes an infected settler is a walker or the wound "
                + "closing badly, never the fever on its own.");

            UndeadRestless = Config.Bind("Undead", "Restless", true,
                "A walker that has neither moved nor swung for a while drops "
                + "its target, its order and its goal, and looks again.");

            UndeadIdleSeconds = Config.Bind("Undead", "IdleSeconds", 30f,
                "How long a walker may stand still before that happens, in "
                + "real seconds. Breaking a wall counts as doing something, so "
                + "this does not interrupt it.");
        }

        private static void PatchUndead(Harmony harmony)
        {
            PatchOne(harmony, typeof(RisenRaidShare));
            PatchOne(harmony, typeof(UndeadInfection));
            PatchOne(harmony, typeof(UndeadInfectionMark));
            PatchOne(harmony, typeof(RisenTraits));
            PatchOne(harmony, typeof(RisenRazeBuildings));
            PatchOne(harmony, typeof(RisenTargetBuildings));
            PatchOne(harmony, typeof(RisenTargetBuildingsUnderOrders));
            PatchOne(harmony, typeof(RisenHostility));
            PatchOne(harmony, typeof(RisenHostileToAll));
            PatchOne(harmony, typeof(RisenChaseFirst));
            PatchOne(harmony, typeof(RisenChaseUnderOrders));
            PatchOne(harmony, typeof(RisenHuntPerception));
            PatchOne(harmony, typeof(RisenFromTheKill));
            PatchOne(harmony, typeof(RisenDisposeGuard));
            PatchOne(harmony, typeof(RisenProximityGuard));
            PatchOne(harmony, typeof(RisenNeverRetreats));
            PatchOne(harmony, typeof(RisenProvokes));
            PatchOne(harmony, typeof(RisenProvokesUnderOrders));
        }

        private static void StartUndead()
        {
            RisenRestless.Start();
            RisenHunt.Start();
            RisenBrawl.Start();
            RisenSighting.Start();
            UndeadInfectionFever.Start();
        }
    }
}
