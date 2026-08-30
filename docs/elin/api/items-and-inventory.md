# Items And Inventory

- `Card.things`, `ThingContainer`, `Chara.Pick(Thing,bool,bool)`, `Chara.DropThing(Thing,int)`, `Card.Destroy()`, `Thing.GetPrice(CurrencyType,bool,PriceType,Chara)`, `Card.Name`, `Card.category`, and `Card.uid` exist (`VERIFIED-METADATA`).
- Runtime capability probe enumerated 33 PC inventory items (`VERIFIED-RUNTIME`).
- Runtime selected `Thing.rarity` from BQ's candidate list as readable item quality source (`VERIFIED-RUNTIME`); whether that equals made-item quality is unresolved (`UNRESOLVED`).
- `TryTransferItem` and `TryDestroyItem` are protected by mutation policy and re-read inventories after calling vanilla, but real transfer/destroy behavior has not been exercised by a committed probe (`VERIFIED-METADATA`, `UNRESOLVED` behavior).
- SourceData `SourceThing.csv` is indexed locally; compact mappings should use row ids, category, value, LV, quality, recipeKey, components, tags, and details only as needed (`SOURCE-DATA`).
