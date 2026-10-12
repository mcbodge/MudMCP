# Plan: Version-accurate parsing for MudBlazor MCP (enums, categories, inheritance, `<inheritdoc/>`)

Replace the syntax-only, hardcoded parsing paths with a **single Roslyn `CSharpCompilation` semantic model** built once per version at index-build time (cached to `index.json`, so runtime tool calls are unaffected). This one change fixes inheritance, `<inheritdoc/>`, and multi-class files together; enums and categories are then derived from the same version's real source. No external metadata references (MudBlazor's base chain binds source-to-source; the chain naturally stops at the external `ComponentBase`).

**Phase 1 — Models & cache (foundation)**
1. `Models/ComponentInfo.cs`: add trailing defaulted `bool IsInherited = false, string? DeclaringType = null` to `ComponentParameter`, `ComponentEvent`, `ComponentMethod` (keeps existing positional test constructors compiling). Add `EnumInfo(Name, Namespace, Summary, Values)` + `EnumValueInfo(Name, Value?, Description)` records.
2. `Services/ComponentIndexer.cs`: add `List<EnumInfo> Enums` to `CachedIndex`; bump `CacheSchemaVersion` 3→4.
3. `Services/IComponentIndexer.cs`: add `GetEnumAsync` / `GetAllEnumsAsync`.

**Phase 2 — Semantic parser (core)** *(depends on P1)*
4. New `Services/Parsing/SemanticComponentParser.cs`: builds one `CSharpCompilation` (`DocumentationMode.Parse`, files parsed in parallel, no refs) over `src/MudBlazor/**/*.cs`. Extracts each component from its **type symbol** — walks `BaseType` chain merging `[Parameter]`/`[CascadingParameter]` props, `EventCallback` props, public methods, tagging `IsInherited` + `DeclaringType`. Resolves docs via `GetDocumentationCommentXml()` plus a manual **`<inheritdoc/>` resolver** (walks overridden/interface/`cref`/same-name base member recursively; also fills empty summaries). Also `ExtractEnums(compilation)` → all public enum symbols (`<summary>` preferred over `[Description]`, explicit value when present).

**Phase 3 — Category parser** *(depends on P1, parallel with P2)*
5. New `Services/Parsing/MenuCategoryParser.cs`: syntax-walks `src/MudBlazor.Docs/Services/Menu/MenuService.cs` `_docsComponents` fluent chain (`AddItem`/`AddNavGroup` + `typeof(...)` incl. child types) → `typeName → categoryName` map. Named groups only (`Form & Inputs`, `Pickers`, `Buttons`, `Charts`, `Functional`); ungrouped items → `"Components"`. Retires `CategoryMapper` + its `InferCategoryFromName` heuristic and the `InitializeAsync(string.Empty)` cached hack.

**Phase 4 — Indexer wiring** *(depends on P2, P3)*
6. `Services/ComponentIndexer.cs`: build compilation + parse menu once per full build; keep directory walk for `dirName`→`DocumentationUrl`/`SourceUrl`, map file→symbol by base name. Per-component semantic extract with **try/catch fallback to `XmlDocParser`** (degraded own-declared only). New `IndexEnumsAsync`. `GetCategoriesAsync`/`GetComponentsByCategoryAsync` reconstruct from `_components`. Persist/load `Enums`.
7. `Program.cs`: register `SemanticComponentParser` + `MenuCategoryParser`; keep `XmlDocParser` (fallback only); drop `CategoryMapper`.

**Phase 5 — Tools** *(depends on P4)*
8. `Tools/ApiReferenceTools.cs`: `get_enum_values` reads `indexer.GetEnumAsync` (delete `GetKnownEnumValues` switch); `get_api_reference` falls back to the enum index on component miss.
9. `Tools/ComponentDetailTools.cs`: implement `includeInheritedMembers` (**default true**; filter to own-declared when false; annotate "(inherited from X)"). `get_component_parameters` shows the merged annotated set (no new flag).

**Phase 6 — Tests & docs** *(depends on P5)*
10. Migrate `XmlDocParserTests`→`SemanticComponentParserTests` (small compilations from snippets: inheritance merge, `<inheritdoc/>`, multi-class); `CategoryMapperTests`→`MenuCategoryParserTests`; update `ApiReferenceToolsTests` enum tests to a mock indexer returning `EnumInfo`; add `ComponentDetailToolsTests` for the inherited-filter/annotation. Light updates to `03-architecture.md` & `05-tools-reference.md`.

**Relevant files**
- `XmlDocParser.cs` — demoted to fallback; reuse its XML-clean helpers in the new parser.
- `CategoryMapper.cs` — replaced by `MenuCategoryParser`.
- `ComponentIndexer.cs` — `IndexSingleComponentFileAsync`/`CreateApiReference`/`TryLoad`/`SaveCachedIndex`/`GetCategoriesAsync` rewired.
- `ComponentInfo.cs`, `ApiReferenceTools.cs`, `ComponentDetailTools.cs`, `Program.cs`.
- Source of truth in clone: `data/v9.0.0/mudblazor-repo/src/MudBlazor/{Enums,Base,Components}`, `.../MudBlazor.Docs/Services/Menu/MenuService.cs`.

**Verification**
1. `dotnet build` (repo-wide `TreatWarningsAsErrors`) — watch xUnit1051 (pass `TestContext.Current.CancellationToken`).
2. `dotnet test `MudBlazor.Mcp.Tests.csproj` -c Release` (MTP).
3. Delete `index.json`, run `--version 9.0.0`, then via MCP: `get_enum_values Color` (dynamic, rich summaries); `get_component_detail MudButton` with `includeInheritedMembers` true (shows `Href`/`ButtonType` annotated "(inherited from MudBaseButton)") vs false (own-only); `list_components` category `Buttons` / `Form & Inputs`; `get_api_reference Color`.
4. Inspect regenerated `index.json`: enums present, members carry `IsInherited`/`DeclaringType`, categories are menu-derived.

**Decisions** (from interview)
- Full semantic-model compilation, no external refs (D1); merge+annotate inheritance, flag default true (D2); dedicated `EnumInfo` index + `get_api_reference` enum fallback, `<summary>` preferred (D3); menu-only categories, ungrouped="Components", heuristic dropped, unlisted=null (D4); `XmlDocParser` kept only as a wired degraded fallback, dropped if unreachable (D5).

**Further Considerations**
1. `ComponentDetailTools` still has a static `knownEnums` set + `GetEnumExampleValue` (more hardcoded-enum debt) used for parameter usage hints. Make it data-driven from the new enum index? **Recommend yes** (folds into gap #1) / defer as follow-up / leave as-is.
2. Parameter category resolution (`[Category(CategoryTypes.Button.ClickAction)]`) is currently imprecise (regex captures `"Button"`). Out of your 4 gaps — **recommend leaving out of scope** / include as a bonus semantic fix.

## Plan notes

### Task (user)
Fix 4 accuracy gaps, best-practice + performance-conscious:
1. Enums hard-coded (GetKnownEnumValues switch in ApiReferenceTools.cs) → parse real enums w/ Roslyn, index like components.
2. Categories hard-coded (CategoryMapper.InitializeKnownCategories, ignores repositoryPath) → derive from version's MenuService.cs.
3. includeInheritedMembers is a no-op (ComponentDetailTools) → implement or drop.
4. <inheritdoc/> not resolved + first-public-class-only (XmlDocParser syntax-only) → use Roslyn Compilation/semantic model. Biggest win.

### Key findings (verified)
- data/ is search-excluded; navigate MudBlazor clone directly via list_dir/read_file. Repo clone at data/v9.0.0/mudblazor-repo/src/MudBlazor.
- Enums: src/MudBlazor/Enums/*.cs (~55). Have XML <summary> + [Description("...")] per member. SOME enums live outside Enums/ (e.g. Breakpoint not in folder). Compilation gets ALL enum symbols for free.
- Categories source: src/MudBlazor.Docs/Services/Menu/MenuService.cs — `_docsComponents` fluent chain: top-level `.AddItem("Name", typeof(MudX<T>), typeof(Child)...)` (UNGROUPED) + `.AddNavGroup("Group", bool, new DocsComponents().AddItem(...))`. v9 groups: "Form & Inputs","Pickers","Buttons","Charts","Functional". Hard-coded mapper invents categories that DON'T exist in v9 (Navigation/Layout/Data Display/Feedback/etc).
- Inheritance: base declared in BOTH .razor (@inherits) and .razor.cs (`: MudBaseButton`). Chain MudButton→MudBaseButton→MudComponentBase all IN cloned source; terminates at ComponentBase (external). MudBaseButton holds HtmlTag/ButtonType/Href/etc [Parameter]s currently MISSING from MudButton output. Partial classes.
- Param category: ExtractCategoryFromAttribute regex `CategoryTypes\.(\w+)` on `CategoryTypes.Button.ClickAction` captures "Button" (wrong-ish). NOT in 4 gaps → out of scope (note as further consideration).
- Models: Models/ComponentInfo.cs — ComponentInfo, ComponentParameter, ComponentEvent, ComponentMethod, MethodParameter, ComponentExample, ComponentCategory, ApiReference, ApiMember. Immutable records. Serialized to index.json.
- Indexer: ComponentIndexer.IndexSingleComponentFileAsync (per-file, XmlDocParser.ParseComponentFileAsync → first public class), then CreateApiReference stored in _apiReferences (ONLY components; enums never indexed → get_api_reference "Color" fails today). CategoryMapper.GetCategoryName ?? InferCategoryFromName fallback.
- Cache: CacheSchemaVersion=3 (ComponentIndexer.cs), CachedIndex record. Bump on model change. index.json persisted per version.
- Enum tool: get_enum_values (ApiReferenceTools) fully static switch, does NOT touch indexer. get_api_reference also has knownEnums set in ComponentDetailTools for usage hints.
- DI: Program.cs RegisterCoreServices ~208-225, all parsers AddSingleton. XmlDocParser, RazorDocParser, ExampleExtractor, CategoryMapper.
- Tests: tests mirror src. XmlDocParserTests, CategoryMapperTests, ApiReferenceToolsTests (asserts static enum values), ComponentDetailToolsTests. xUnit1051: pass TestContext.Current.CancellationToken (named form if optional params precede).
- Build: TreatWarningsAsErrors repo-wide. MTP tests. Copyright header on every file.

### Roslyn approach notes
- Build ONE CSharpCompilation from src/MudBlazor/**/*.cs (+ .razor.cs). ParseOptions DocumentationMode.Parse for doc comments. NO external metadata refs needed (MudBlazor-internal base chain binds source-to-source; attribute detection by name; GetDocumentationCommentXml works on source). Chain naturally stops at external ComponentBase.
- Partial-class merge is automatic (big win). Multi-class files: iterate all type symbols, no more "first public class".
- <inheritdoc/> NOT auto-expanded by GetDocumentationCommentXml → resolve manually: if doc empty or has <inheritdoc [cref]> → walk OverriddenMember / interface member / same-name base member (or cref target) recursively until real summary.
- Inheritance merge: walk INamedTypeSymbol.BaseType collecting [Parameter]/[CascadingParameter] props, EventCallback props, public methods; tag DeclaringType + IsInherited.
- One-time cost at index build (already ~50s clone), cached to index.json → runtime tool calls unaffected.
- MenuService.cs parsed SEPARATELY syntax-only (InvocationExpression + TypeOfExpression walk).

### DECISIONS (confirmed via interview)
- D1 architecture: **Full CSharpCompilation semantic model, NO external metadata refs.** ParseOptions DocumentationMode.Parse. One compilation over src/MudBlazor/**/*.cs. Partial-merge auto; iterate all type symbols (no first-public-class).
- D2 inheritance: **Merge full base chain; tag each member IsInherited + DeclaringType.** includeInheritedMembers **default TRUE**; when false → filter to own-declared only. Inherited shown annotated "(inherited from X)".
- D3 enums: **Dedicated EnumInfo model + _enums index (cached).** Built from ALL public enum symbols in compilation. get_enum_values reads it (drop static switch). get_api_reference falls back to enum index. Description prefers <summary>, falls back to [Description]. Include explicit numeric value when present.
- D4 categories: **Parse MenuService.cs (syntax) → menu-only categories.** Named v9 groups: Form & Inputs / Pickers / Buttons / Charts / Functional. Top-level ungrouped bucket = "Components". DROP InferCategoryFromName heuristic. Menu-absent components → null category. Child typeof types inherit their item's category. Non-Mud chart internals ignored (not indexed).
- D5 XmlDocParser: **Keep ONLY as a wired degraded fallback** (per-component try/catch: on semantic-extract exception/empty → XmlDocParser.ParseComponentFileAsync(primary file), own-declared only). If impl shows it's unreachable/not useful, DROP it. Keep/trim its tests accordingly.

### Self-decided impl details (documented, non-blocking)
- Add IsInherited(=false)+DeclaringType(=null) as TRAILING defaulted params on ComponentParameter/Event/Method → existing positional test ctors still compile.
- New records EnumInfo(Name,Namespace,Summary,Values) + EnumValueInfo(Name,Value?,Description). Add to CachedIndex. Bump CacheSchemaVersion 3→4.
- Replace CategoryMapper with build-time MenuCategoryParser. GetCategoriesAsync/GetComponentsByCategoryAsync reconstruct from _components grouped by Category (removes the InitializeAsync(string.Empty) cached hack). ComponentCategory Title=group name, Description=null.
- Keep directory-driven component discovery (for dirName→DocumentationUrl/SourceUrl) but map file→type symbol by base name (handles MudList<T>→MudList.razor.cs).
- get_component_parameters + get_api_reference show merged set annotated, NO new flag (only get_component_detail gets the flag). 
- Optional/Further: make ComponentDetailTools static knownEnums/GetEnumExampleValue data-driven from enum index (also hardcoded-enum debt). Mark optional.