# 2026-11-16: the inventory shows the bar a consumable fills

Bruno, with a shot of the inventory on the ORZABAL CAPSULE:

```text
consumables (when the consumable is selected) should show me how full the mk power is in the ineventory screen itself
```

A request: the item panel showed only the words.

Now a gauge stands under the words of the selected consumable (`InventoryUI.DrawGauge`):

| Selected | Gauge |
|---|---|
| An MK capsule (mk_warspite, mk_erebus, mk_orzabal, mk_edgar) | that power's bar, for example "mk_orzabal  45 / 100" |
| The SHORT capsule | the SHORT bar, in yellow |
| The second wind pill | "SECOND WIND  ready" or "used", no bar |
| Any other item | nothing |

The gauge follows the use at once: a capsule taken in the inventory fills it before her eyes.

NOT COMPILED, NOT RUN (no free RAM for Unity).
