# Interface screenshots and animation

The main animation and screenshots come from a continuous screen recording of the installed SMP Intelligence Windows prototype on November 17, 2026. The original interface, company logo, and live responses are retained. This session used the prototype's OpenAI and SQL Server connection.

Company part numbers are blurred after recording, including in the conversation, tables, and home-screen example. Internal usernames in the cross-reference table and all visible Canada, Mexico, US, and VIO values in the applications table are also blurred. Column headings remain readable. The input OE number `12692048` remains visible. The session returned two matching parts and 117 vehicle application rows.

- `smp-demo.gif`: normal-speed excerpts from the continuous recording: the typed OE lookup, matching-parts table, follow-up “what applications does it fit?”, and applications table.
- `smp.chat.png`: a redacted frame showing the live cross-reference response.
- `smp.results.png`: a redacted frame showing the live applications table.
- `smp.sql.png`: an earlier, separately prepared illustration with a fictional query and neutral branding. It is not a frame from this recording.

Idle pauses are trimmed for the cover animation; typing and interaction footage remain at normal speed. The animation is a workflow demonstration, not a response-time benchmark. The raw recording is not published.

The current repository contains sanitized original source and offline tests. The earlier browser sample has been retired. See the [source guide](../docs/source-guide.md).

The [two-minute technical walkthrough](https://kuvelet.github.io/portfolio/walkthrough/) combines this redacted app footage with a separately recorded live SQL inspection, the then-current public sample code, and the results of a local run of its 105 tests on November 17, 2026. It preserves the same redactions and also blurs the internal physical table mapping in the SQL details. The video, captions, narration script, and watch page are hosted in the portfolio repository.

The walkthrough now includes an AI-generated stock voice (Kokoro, Michael), normalized speech audio, and matching English subtitles and a transcript. The original redacted video stream is preserved without re-encoding. Voiceover production used [Kokoro](https://huggingface.co/hexgrad/Kokoro-82M) locally; no company data or voice recording was uploaded for speech generation.

The video is an unchanged historical recording: its sample code, test total, and final browser-demo invitation predate the replacement with original source. The app footage remains representative of the internal prototype; use the current README and source guide for the repository contents.
