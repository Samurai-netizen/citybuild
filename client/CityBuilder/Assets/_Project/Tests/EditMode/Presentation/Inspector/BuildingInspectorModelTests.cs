#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Presentation.Inspector;
using CityBuilder.Client.Presentation.Ui;
using CityBuilder.Client.Tests.EditMode.Presentation.Hud;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Inspector
{
    /// <summary>
    /// The words and flags of the building inspector (M0-51): status and time, the recipe with rates at the current staffing, the worker stepper
    /// (limits, the pending value and the preview rate) and the upgrade section (cost, time, gain and every reason it is disabled).
    /// </summary>
    public sealed class BuildingInspectorModelTests
    {
        /// <summary>No command on its way.</summary>
        private static readonly InspectorPending Nothing = new InspectorPending(null, false);

        /// <summary>The real content of the fixtures.</summary>
        private GameContent _content = null!;

        /// <summary>Before each test: the content.</summary>
        [SetUp]
        public void LoadContent()
        {
            _content = HudTestData.Content();
        }

        /// <summary>
        /// The model of the building with id 1 in a city of <paramref name="buildings"/> at <paramref name="clock"/>; the building with id 1
        /// must be among them.
        /// </summary>
        private BuildingInspectorModel Model(
            Building[] buildings,
            InspectorPending pending = default,
            long clock = 0,
            long coins = 700,
            long logs = 40,
            long planks = 10,
            string[]? research = null,
            CommandAvailability availability = default)
        {
            CityState city = HudTestData.City(clock, coins, logs, planks, buildings, research);
            Assert.That(city.TryGetBuilding(1, out Building? building), Is.True);
            BuildingInspectorModel? model = BuildingInspectorModel.Create(_content, city, building!, pending, availability);
            Assert.That(model, Is.Not.Null);
            return model!;
        }

        /// <summary>An idle building of <paramref name="typeId"/> with id 1 at <paramref name="level"/> with <paramref name="workers"/> workers.</summary>
        private static Building Idle(string typeId, int workers, int level = 1)
        {
            return new Building(1, new BuildingTypeId(typeId), 0, 0, level, workers, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>A sawmill (id 1) at level 1 running a cycle with three workers: 180 worker-seconds, so 60 s.</summary>
        private static Building BusySawmill() => HudTestData.Producer(1, 0, "sawmill", 3, 180);

        /// <summary>A cottage with id 2 on another tile: its four workers are the city's.</summary>
        private static Building Cottage() => HudTestData.House(2, 1);

        /// <summary>A running sawmill says its name, level, status, time to the next batch, recipe and rates at its three workers.</summary>
        [Test]
        public void Sawmill_Producing_ShowsNameLevelStatusTimeRecipeAndRates()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() });

            Assert.That(model.BuildingId, Is.EqualTo(1));
            Assert.That(model.Name, Is.EqualTo("Sawmill"));
            Assert.That(model.LevelText, Is.EqualTo("Level 1 of 2"));
            Assert.That(model.StatusText, Is.EqualTo("Producing"));
            Assert.That(model.TimeText, Is.EqualTo("Next batch in 1m"));
            Assert.That(model.RecipeText, Is.EqualTo("2 Logs → 1 Planks"));
            Assert.That(model.RatesText, Is.EqualTo("Uses 120 Logs/h · Makes 60 Planks/h"));
            Assert.That(model.WorkersText, Is.EqualTo("3 / 3"));
        }

        /// <summary>The time to the next batch follows the clock the screen shows.</summary>
        [Test]
        public void TimeText_FollowsTheDisplayedClock()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() }, clock: 45);

            Assert.That(model.TimeText, Is.EqualTo("Next batch in 15s"));
        }

        /// <summary>A producer with no input has no "Uses" part and says "Nothing" on the left of the arrow.</summary>
        [Test]
        public void Woodcutter_HasNoInputsAndNoUsesPart()
        {
            BuildingInspectorModel model = Model(new[] { HudTestData.Producer(1, 0, "woodcutter", 2, 60), Cottage() });

            Assert.That(model.RecipeText, Is.EqualTo("Nothing → 1 Logs"));
            Assert.That(model.RatesText, Is.EqualTo("Makes 120 Logs/h"));
        }

        /// <summary>A completed research that cuts the labor shows in the rates, as in the simulator.</summary>
        [Test]
        public void Rates_FollowTheCitysResearch()
        {
            BuildingInspectorModel model = Model(
                new[] { Idle("woodcutter", 2), Cottage() }, research: new[] { "sharp_axes" });

            // Labor 60 less 20 % is 48; with 2 workers a cycle is 24 s, so 150 logs an hour.
            Assert.That(model.RatesText, Is.EqualTo("Makes 150 Logs/h"));
        }

        /// <summary>An idle producer that waits for an input names the item.</summary>
        [Test]
        public void Status_MissingInput_NamesTheItem()
        {
            BuildingInspectorModel model = Model(new[] { Idle("sawmill", 3), Cottage() }, logs: 1);

            Assert.That(model.StatusText, Is.EqualTo("Waiting for Logs"));
            Assert.That(model.TimeText, Is.Empty);
        }

        /// <summary>An idle producer whose output does not fit the storage says which one is full.</summary>
        [Test]
        public void Status_StorageFull_NamesTheItem()
        {
            BuildingInspectorModel model = Model(new[] { Idle("sawmill", 3), Cottage() }, planks: 500);

            Assert.That(model.StatusText, Is.EqualTo("Planks storage is full"));
        }

        /// <summary>A producer with no workers says so, whether it is idle or has a frozen cycle (which has no end to count to).</summary>
        [Test]
        public void Status_NoWorkers_IdleAndFrozen()
        {
            var frozen = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, 0, BuildingPhase.Producing, 0, null, 0, 180, 180, 0);

            Assert.That(Model(new[] { Idle("sawmill", 0), Cottage() }).StatusText, Is.EqualTo("No workers assigned"));
            BuildingInspectorModel model = Model(new[] { frozen, Cottage() });
            Assert.That(model.StatusText, Is.EqualTo("No workers assigned"));
            Assert.That(model.TimeText, Is.Empty);
        }

        /// <summary>An idle producer that can start its next cycle is ready, not stalled.</summary>
        [Test]
        public void Status_IdleAndAbleToRun_IsReady()
        {
            Assert.That(Model(new[] { Idle("sawmill", 3), Cottage() }).StatusText, Is.EqualTo("Ready"));
        }

        /// <summary>A building under construction says so and counts down to its end.</summary>
        [Test]
        public void Status_Constructing_CountsDown()
        {
            var site = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, 0, BuildingPhase.Constructing, 0, 120, 0, 0, 0, 0);

            BuildingInspectorModel model = Model(new[] { site, Cottage() }, clock: 20);

            Assert.That(model.StatusText, Is.EqualTo("Under construction"));
            Assert.That(model.TimeText, Is.EqualTo("Ready in 1m 40s"));
            Assert.That(model.RecipeText, Is.EqualTo("2 Logs → 1 Planks"));
            Assert.That(model.RatesText, Is.Empty);
            Assert.That(model.HasStepper, Is.False);
        }

        /// <summary>An upgrade names the level it reaches and counts down; the stepper stays, as the server accepts workers meanwhile.</summary>
        [Test]
        public void Status_Upgrading_NamesTheTargetLevel()
        {
            var upgrading = new Building(1, new BuildingTypeId("woodcutter"), 0, 0, 1, 2, BuildingPhase.Upgrading, 0, 200, 2, 0, 0, 0);

            BuildingInspectorModel model = Model(new[] { upgrading, Cottage() }, clock: 100);

            Assert.That(model.StatusText, Is.EqualTo("Upgrading to level 2"));
            Assert.That(model.TimeText, Is.EqualTo("Upgrade done in 1m 40s"));
            Assert.That(model.HasStepper, Is.True);
        }

        /// <summary>A cottage provides workers, has no rates and no stepper, and upgrades to more workers.</summary>
        [Test]
        public void House_ProvidesWorkers_HasNoStepper()
        {
            BuildingInspectorModel model = Model(new[] { HudTestData.House(1, 0) });

            Assert.That(model.Name, Is.EqualTo("Cottage"));
            Assert.That(model.RecipeText, Is.EqualTo("Provides 4 workers"));
            Assert.That(model.RatesText, Is.Empty);
            Assert.That(model.HasStepper, Is.False);
            Assert.That(model.StatusText, Is.EqualTo("Ready"));
            Assert.That(model.UpgradeGainText, Is.EqualTo("Provides 4 → 6 workers"));
            Assert.That(model.UpgradeCostText, Is.EqualTo("Cost: 120 Coins, 30 Logs"));
            Assert.That(model.UpgradeTimeText, Is.EqualTo("Takes 1m 30s"));
        }

        /// <summary>At the level's most workers + is off and says why; − still works.</summary>
        [Test]
        public void Stepper_AtTheMaximum_PlusIsOff()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() });

            Assert.That(model.HasStepper, Is.True);
            Assert.That(model.MinusEnabled, Is.True);
            Assert.That(model.PlusEnabled, Is.False);
            Assert.That(model.StepperNote, Is.EqualTo("At most 3 workers at this level"));
            Assert.That(model.PreviewText, Is.Empty);
        }

        /// <summary>With no workers − is off and + works while the city has free workers; there is no note.</summary>
        [Test]
        public void Stepper_AtZero_MinusIsOff()
        {
            BuildingInspectorModel model = Model(new[] { Idle("sawmill", 0), Cottage() });

            Assert.That(model.WorkersText, Is.EqualTo("0 / 3"));
            Assert.That(model.MinusEnabled, Is.False);
            Assert.That(model.PlusEnabled, Is.True);
            Assert.That(model.StepperNote, Is.Empty);
        }

        /// <summary>When every worker of the city is assigned, + is off and says there are no free workers.</summary>
        [Test]
        public void Stepper_WithNoFreeWorkers_PlusIsOff()
        {
            var woodcutter = new Building(3, new BuildingTypeId("woodcutter"), 2, 0, 1, 2, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

            // One cottage gives 4 workers; the sawmill has 2 and the woodcutter 2.
            BuildingInspectorModel model = Model(new[] { Idle("sawmill", 2), Cottage(), woodcutter });

            Assert.That(model.WorkersText, Is.EqualTo("2 / 3"));
            Assert.That(model.PlusEnabled, Is.False);
            Assert.That(model.MinusEnabled, Is.True);
            Assert.That(model.StepperNote, Is.EqualTo("No free workers"));
        }

        /// <summary>A target on its way is the stepper's value; the rates stay the current ones and a preview shows the rates at the target.</summary>
        [Test]
        public void Stepper_PendingTarget_ShowsItAndPreviewsTheRate()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() }, new InspectorPending(2, false));

            Assert.That(model.IsWorkersPending, Is.True);
            Assert.That(model.WorkersTarget, Is.EqualTo(2));
            Assert.That(model.WorkersText, Is.EqualTo("2 / 3"));
            Assert.That(model.RatesText, Is.EqualTo("Uses 120 Logs/h · Makes 60 Planks/h"));
            Assert.That(model.PreviewText, Is.EqualTo("With 2 workers: Uses 80 Logs/h · Makes 40 Planks/h"));
            Assert.That(model.StepperNote, Is.EqualTo("Updating workers…"));
            Assert.That(model.PlusEnabled, Is.True);
            Assert.That(model.MinusEnabled, Is.True);
        }

        /// <summary>The preview says "worker" for one, shows the rate of zero workers as zero, and + and − follow the pending value.</summary>
        [Test]
        public void Stepper_PendingOneAndZero_PreviewAndButtons()
        {
            BuildingInspectorModel one = Model(new[] { BusySawmill(), Cottage() }, new InspectorPending(1, false));
            BuildingInspectorModel zero = Model(new[] { BusySawmill(), Cottage() }, new InspectorPending(0, false));

            Assert.That(one.PreviewText, Is.EqualTo("With 1 worker: Uses 40 Logs/h · Makes 20 Planks/h"));
            Assert.That(zero.PreviewText, Is.EqualTo("With 0 workers: Uses 0 Logs/h · Makes 0 Planks/h"));
            Assert.That(zero.MinusEnabled, Is.False);
            Assert.That(zero.PlusEnabled, Is.True);
        }

        /// <summary>The preview at the pending value uses the engine's rates, research included.</summary>
        [Test]
        public void Stepper_Preview_FollowsTheCitysResearch()
        {
            BuildingInspectorModel model = Model(
                new[] { Idle("woodcutter", 2), Cottage() }, new InspectorPending(1, false), research: new[] { "sharp_axes" });

            Assert.That(model.RatesText, Is.EqualTo("Makes 150 Logs/h"));
            Assert.That(model.PreviewText, Is.EqualTo("With 1 worker: Makes 75 Logs/h"));
        }

        /// <summary>A pending target equal to the assigned count has no preview but is still pending.</summary>
        [Test]
        public void Stepper_PendingTargetEqualToAssigned_HasNoPreview()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() }, new InspectorPending(3, false));

            Assert.That(model.IsWorkersPending, Is.True);
            Assert.That(model.PreviewText, Is.Empty);
        }

        /// <summary>A target already above the assigned count has used free workers: + counts one more against what is left.</summary>
        [Test]
        public void Stepper_PendingIncrease_UsesUpTheFreeWorkers()
        {
            // A cottage gives 4 workers and a level 2 sawmill (6 at most) has 2, so 2 are free.
            Building sawmill = Idle("sawmill", 2, level: 2);

            BuildingInspectorModel three = Model(new[] { sawmill, HudTestData.House(2, 1) }, new InspectorPending(3, false));
            BuildingInspectorModel four = Model(new[] { sawmill, HudTestData.House(2, 1) }, new InspectorPending(4, false));

            Assert.That(three.PlusEnabled, Is.True);
            Assert.That(four.PlusEnabled, Is.False);
            Assert.That(four.StepperNote, Is.EqualTo("Updating workers…"));
            Assert.That(four.WorkersText, Is.EqualTo("4 / 6"));
        }

        /// <summary>A pending target outside the level's range is held to it, so the stepper never shows an impossible value.</summary>
        [Test]
        public void Stepper_PendingTargetOutsideTheRange_IsClamped()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() }, new InspectorPending(9, false));

            Assert.That(model.WorkersTarget, Is.EqualTo(3));
            Assert.That(model.WorkersText, Is.EqualTo("3 / 3"));
        }

        /// <summary>The upgrade section names the next level, the price, the time and the gain.</summary>
        [Test]
        public void Upgrade_ShowsTitleCostTimeAndGain()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() }, planks: 30);

            Assert.That(model.UpgradeTitle, Is.EqualTo("Upgrade to level 2"));
            Assert.That(model.UpgradeCostText, Is.EqualTo("Cost: 250 Coins, 30 Planks"));
            Assert.That(model.UpgradeTimeText, Is.EqualTo("Takes 3m"));
            Assert.That(model.UpgradeGainText, Is.EqualTo("Max workers 3 → 6"));
            Assert.That(model.CanUpgrade, Is.True);
            Assert.That(model.UpgradeNote, Is.Empty);
        }

        /// <summary>A producer that is running can be upgraded; the server ends its cycle.</summary>
        [Test]
        public void Upgrade_OfAProducingBuilding_IsAllowed()
        {
            Assert.That(Model(new[] { BusySawmill(), Cottage() }, planks: 30).CanUpgrade, Is.True);
        }

        /// <summary>An upgrade the stock cannot pay says what is missing, every item.</summary>
        [Test]
        public void Upgrade_WithoutTheItems_NamesWhatIsMissing()
        {
            Assert.That(Model(new[] { BusySawmill(), Cottage() }, planks: 10).UpgradeNote, Is.EqualTo("Not enough Planks"));
            BuildingInspectorModel both = Model(new[] { BusySawmill(), Cottage() }, coins: 100, planks: 0);
            Assert.That(both.UpgradeNote, Is.EqualTo("Not enough Coins, Planks"));
            Assert.That(both.CanUpgrade, Is.False);
        }

        /// <summary>No free builder comes before missing items, as the server checks.</summary>
        [Test]
        public void Upgrade_WithAllBuildersBusy_SaysSoBeforeMissingItems()
        {
            var site1 = new Building(2, new BuildingTypeId("house"), 1, 0, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0);
            var site2 = new Building(3, new BuildingTypeId("house"), 2, 0, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0);

            BuildingInspectorModel model = Model(new[] { BusySawmill(), site1, site2 }, planks: 0);

            Assert.That(model.UpgradeNote, Is.EqualTo("All builders are busy"));
            Assert.That(model.CanUpgrade, Is.False);
        }

        /// <summary>A building that is under construction or already upgrading cannot be upgraded, and that comes before the other reasons.</summary>
        [Test]
        public void Upgrade_OfABusyBuilding_IsOff()
        {
            var upgrading = new Building(1, new BuildingTypeId("woodcutter"), 0, 0, 1, 2, BuildingPhase.Upgrading, 0, 200, 2, 0, 0, 0);
            var site = new Building(1, new BuildingTypeId("sawmill"), 0, 0, 1, 0, BuildingPhase.Constructing, 0, 120, 0, 0, 0, 0);

            Assert.That(Model(new[] { upgrading, Cottage() }, planks: 0).UpgradeNote, Is.EqualTo("Busy until the current job is done"));
            Assert.That(Model(new[] { site, Cottage() }, planks: 0).UpgradeNote, Is.EqualTo("Busy until the current job is done"));
        }

        /// <summary>At the highest level the section says so, has no price, and the button is off without a warning.</summary>
        [Test]
        public void Upgrade_AtTheHighestLevel_SaysSo()
        {
            BuildingInspectorModel model = Model(new[] { Idle("sawmill", 3, level: 2), Cottage() });

            Assert.That(model.LevelText, Is.EqualTo("Level 2 of 2"));
            Assert.That(model.UpgradeTitle, Is.EqualTo("Highest level reached"));
            Assert.That(model.UpgradeCostText, Is.Empty);
            Assert.That(model.UpgradeTimeText, Is.Empty);
            Assert.That(model.UpgradeGainText, Is.Empty);
            Assert.That(model.CanUpgrade, Is.False);
            Assert.That(model.UpgradeNote, Is.Empty);
        }

        /// <summary>An upgrade on its way turns the button off with a note, so a second tap cannot ask again.</summary>
        [Test]
        public void Upgrade_Pending_IsOffWithANote()
        {
            BuildingInspectorModel model = Model(new[] { BusySawmill(), Cottage() }, new InspectorPending(null, true), planks: 30);

            Assert.That(model.IsUpgradePending, Is.True);
            Assert.That(model.CanUpgrade, Is.False);
            Assert.That(model.UpgradeNote, Is.EqualTo("Upgrade requested…"));
        }

        /// <summary>A building of a type or a level the content does not know gets no model: the screen shows nothing and nothing throws.</summary>
        [Test]
        public void Create_UnknownTypeOrLevel_ReturnsNull()
        {
            CityState city = HudTestData.City(0, 700, 40, 10, new[] { Idle("sawmill", 0, level: 3) });
            var unknown = new Building(2, new BuildingTypeId("castle"), 5, 5, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);

            Assert.That(BuildingInspectorModel.Create(_content, city, city.Buildings[0], Nothing, CommandAvailability.Allowed), Is.Null);
            Assert.That(BuildingInspectorModel.Create(_content, city, unknown, Nothing, CommandAvailability.Allowed), Is.Null);
        }

        /// <summary>A missing argument is a programmer error.</summary>
        [Test]
        public void Create_NullArguments_Throw()
        {
            CityState city = HudTestData.City(0, 700, 40, 10, new[] { BusySawmill() });

            Assert.Throws<ArgumentNullException>(() => BuildingInspectorModel.Create(null!, city, city.Buildings[0], Nothing, CommandAvailability.Allowed));
            Assert.Throws<ArgumentNullException>(() => BuildingInspectorModel.Create(_content, null!, city.Buildings[0], Nothing, CommandAvailability.Allowed));
            Assert.Throws<ArgumentNullException>(() => BuildingInspectorModel.Create(_content, city, null!, Nothing, CommandAvailability.Allowed));
        }

        /// <summary>Two models that say the same are equal (the screen redraws nothing); a different pending state or clock makes them differ.</summary>
        [Test]
        public void Equality_FollowsEveryTextAndFlag()
        {
            Building[] buildings = { BusySawmill(), Cottage() };

            BuildingInspectorModel first = Model(buildings);
            BuildingInspectorModel same = Model(buildings);

            Assert.That(same, Is.EqualTo(first));
            Assert.That(same.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            Assert.That(Model(buildings, new InspectorPending(2, false)), Is.Not.EqualTo(first));
            Assert.That(Model(buildings, new InspectorPending(null, true)), Is.Not.EqualTo(first));
            Assert.That(Model(buildings, clock: 1), Is.Not.EqualTo(first));
            Assert.That(first.Equals(null), Is.False);
        }
    }
}
