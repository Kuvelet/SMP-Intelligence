# Interface screenshots and animation

These assets render the original WPF interface from an isolated capture build. The public capture replaces the company logo, name in the interface, and contact footer with neutral “Catalog Intelligence” branding. Layout, tabs, conversation bubbles, and table controls come from the original client.

All result records and vehicle applications are fictional. The input OE number `12692048` is used at the author’s request; the illustrated matches and applications are not verified fitment for it. Returned part-number cells are blurred in the WPF rendering before frames are saved.

- `smp-demo.gif`: the typed OE lookup, matching-parts table, exact follow-up “what applications does it fit?”, and applications table.
- `smp.chat.png`: the fictional cross-reference response.
- `smp.results.png`: the fictional applications table with blurred part numbers.
- `smp.sql.png`: a clearly labeled illustrative query in the original query-inspection tab.

The animation uses prepared responses and does not call a production database or an AI service. Frame timing is edited for a roughly 16-second loop, not a response-time benchmark. The separately linked browser code sample has a different interface and uses its own offline question planner and SQLite fixture.
