# Review of #165: letter templates and the PDF engine

- **PR:** https://github.com/Aliator/MedApp/pull/165, branch `feature/letter-templates`
- **Reviewed:** commit `22e002a6` against `main` at `4b036a39`, 18 November 2026 (a quick review: what would hurt)
- **What it does:** PR 15 of `docs/centre-phases-3-5-plan.md`. Letter templates (`LetterTemplates`, migration `AddedLetterTemplates`, `api/letter-templates`, `ManageSetup` to change, `UseApp` to read), the fixed list of merge fields (identity and administrative data only), and the PDF engine (`PdfSharpLetterRenderer`: MigraDoc, Markdig, the bundled Atkinson Hyperlegible font) with a preview of made-up details sent as a download. The web app's Letter Templates list and editor.
- **Licences confirmed:** PDFsharp-MigraDoc 6.2.4 is MIT (6.5.0 and later are AGPL-3.0; Dependabot ignores PDFsharp 6.3 and later); Markdig 1.6.0 is BSD-2-Clause; Atkinson Hyperlegible is the SIL OFL 1.1, unmodified, with its licence and notice beside it.

## Findings

No critical or high findings.

- **Medium, fixed:** `MedApp.Infrastructure/Letters/MarkdownToMigraDoc.cs:39-125` (as reviewed): `AddBlock` and `AddList` recursed once per level of quote or list and indented 0.75 cm per level with no cap, so a 20,000-character body of `>>>>…` or nested `- - -` could overflow the stack (ending the API process) or indent text off the page. Now nesting stops at `MaxDepth` (8): deeper blocks are walked with a stack and laid out as plain paragraphs at the cap's indent; inline nesting stops at `MaxInlineDepth` (16) as plain text; and input Markdig itself refuses as too deeply nested (it throws `ArgumentException` past its own limit, which would have been a 500) is shown as its plain lines. Tests render 5,000 nested `>` and 5,000 nested list items, and a full 20,000-character body of quotes, without throwing (`PdfSharpLetterRendererTests`).
- **Low, fixed:** `MedApp.Application/Letters/Common/LetterTemplateValidation.cs` (`Text`): the merge field and font scan ran even after the length check failed, over text of any size. The rule now stops at the length check (`Cascade(CascadeMode.Stop)`), and create, update and preview take at most 256 KB (`[RequestSizeLimit]`, `LetterTemplatesController.MaxRequestBytes`). Tests check both.

## Remaining risk

- **PDF/A isn't claimed:** veraPDF passes PDFsharp's PDF/A-1a output only while all text is Windows-1252; other characters (Ł, ş, ğ) fail clause 6.3.5, as PDFsharp 6.2 writes no CIDSet (`docs/todo.md`).
- **Tags are coarse:** one paragraph element per page, so headings and lists aren't tagged as such.
- **PDFsharp is pinned below its AGPL releases:** fixes after 6.2.x need a licence decision.
