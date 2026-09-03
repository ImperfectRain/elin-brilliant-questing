using System;
using System.Collections.Generic;
using BrilliantQuesting.Lab.Cli.Scenarios;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>
    /// The one place that knows which scenarios exist. Registration is a single line per scenario;
    /// ids and aliases are indexed here so nothing else has to match names.
    /// </summary>
    public sealed class LabCatalog
    {
        /// <summary>The scenario a bare invocation runs: <c>dotnet run --project ...</c> with no command.</summary>
        public const string DefaultScenarioId = "theft";

        private readonly List<LabScenario> _scenarios = new List<LabScenario>();
        private readonly Dictionary<string, LabScenario> _byName =
            new Dictionary<string, LabScenario>(StringComparer.OrdinalIgnoreCase);

        public LabCatalog(IEnumerable<LabScenario> scenarios)
        {
            if (scenarios == null)
            {
                throw new ArgumentNullException(nameof(scenarios));
            }

            foreach (LabScenario scenario in scenarios)
            {
                Register(scenario);
            }
        }

        /// <summary>Every scenario the laboratory ships. Add a new experiment here and nowhere else.</summary>
        public static LabCatalog Default()
        {
            return new LabCatalog(new LabScenario[]
            {
                new TheftLaboratoryScenario(),
                new QuestlineScenario(),
                new QuestlineSweepScenario(),
                new AmbientScenario(),
                new NewsScenario(),
                new GuildsScenario(),
                new AuthorityScenario(),
                new PlaygroundScenario(),
                new PlaygroundContrastScenario(),
                new PlaygroundSystemsScenario(),
                new IntegrationScenario(),
                new SeedProbeScenario()
            });
        }

        /// <summary>Registration order, which is also the order <c>list</c> prints.</summary>
        public IReadOnlyList<LabScenario> Scenarios => _scenarios;

        /// <summary>Resolves an id or a historic alias. Null when nothing matches.</summary>
        public LabScenario Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return _byName.TryGetValue(name.Trim(), out LabScenario scenario) ? scenario : null;
        }

        public LabScenario DefaultScenario => Find(DefaultScenarioId);

        private void Register(LabScenario scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (string.IsNullOrWhiteSpace(scenario.Id))
            {
                throw new InvalidOperationException(scenario.GetType().Name + " has no id.");
            }

            Claim(scenario.Id, scenario);
            foreach (string alias in scenario.Aliases)
            {
                Claim(alias, scenario);
            }

            _scenarios.Add(scenario);
        }

        private void Claim(string name, LabScenario scenario)
        {
            if (_byName.TryGetValue(name, out LabScenario existing))
            {
                throw new InvalidOperationException(
                    "Lab scenario name '" + name + "' is claimed by both " + existing.Id + " and " + scenario.Id + ".");
            }

            _byName[name] = scenario;
        }
    }
}
