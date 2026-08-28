using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// A small, engine-neutral description of an action Elin says already happened.
    ///
    /// The plugin owns the messy job of reading ActPerformed payloads. Core only sees the parts
    /// the narrative layer can reason about: who acted, who or what was targeted, the real item
    /// involved, and where it happened.
    /// </summary>
    public sealed class ObservedVanillaAction
    {
        public ObservedVanillaAction(
            ObservedVanillaActionKind kind,
            EntityId actor,
            EntityId target,
            EntityId item,
            string itemName,
            EntityId zone,
            string sourceActionId)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Item = item;
            ItemName = itemName ?? string.Empty;
            Zone = zone;
            SourceActionId = sourceActionId ?? string.Empty;
        }

        public ObservedVanillaActionKind Kind { get; }

        public EntityId Actor { get; }

        public EntityId Target { get; }

        public EntityId Item { get; }

        public string ItemName { get; }

        public EntityId Zone { get; }

        public string SourceActionId { get; }
    }

    public enum ObservedVanillaActionKind
    {
        Theft
    }
}
