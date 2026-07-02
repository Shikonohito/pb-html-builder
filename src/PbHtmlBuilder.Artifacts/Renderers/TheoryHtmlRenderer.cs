using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PbHtmlBuilder.Application.Projects;
using PbHtmlBuilder.Domain.Documents;

namespace PbHtmlBuilder.Artifacts.Renderers;

public sealed class TheoryHtmlRenderer : ITheoryHtmlRenderer
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string Render(TheoryDocument document)
    {
        var manifest = CreateManifest(document);
        var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);

        return $$"""
            <!doctype html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{{Text(document.DocumentTitle)}} - PbHtmlBuilder</title>
              <link rel="icon" href="data:,">
              <style>
            {{StudentCss}}
              </style>
            </head>
            <body>
              <div class="builder-workspace" id="appShell">
                <header class="builder-topbar">
                  <button class="builder-icon-button" type="button" id="sidebarToggle" title="Toggle sidebar" aria-label="Toggle sidebar" aria-expanded="true">
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M4 6h16"></path>
                      <path d="M4 12h16"></path>
                      <path d="M4 18h16"></path>
                    </svg>
                  </button>

                  <div class="builder-topic-title">
                    <span>{{Text(document.Brand.TopicKicker)}}</span>
                    <h1>{{Text(document.DocumentTitle)}}</h1>
                  </div>
                </header>

                <aside class="builder-sidebar" aria-label="Оглавление">
                  <div class="builder-sidebar-head">
                    <span>Оглавление</span>
                    <button class="builder-panel-button" type="button" id="innerToggle" title="Collapse sidebar" aria-label="Collapse sidebar">
                      <svg viewBox="0 0 24 24" aria-hidden="true">
                        <path d="m15 18-6-6 6-6"></path>
                      </svg>
                    </button>
                  </div>

                  <nav class="builder-outline" aria-label="Оглавление">
            {{RenderOutlineItems(document)}}
                  </nav>

                  <div class="builder-sidebar-footer">{{Text(document.Brand.Copyright)}}</div>
                </aside>

                <main class="builder-main" aria-label="Theory">
                  <section id="section-map" class="builder-section-map" aria-labelledby="sectionMapTitle">
                    <div>
                      <span>Разделы темы</span>
                      <h3 id="sectionMapTitle">Что важно рассмотреть</h3>
                    </div>
                    <div class="builder-section-map-grid">
            {{RenderMapCells(document)}}
                    </div>
                  </section>

                  <article class="builder-section-skeleton">
            {{RenderSections(document)}}
                  </article>
                </main>
              </div>

              <script id="pb-html-builder-manifest" type="application/json">{{manifestJson}}</script>
              <script>
            {{StudentScript}}
              </script>
            </body>
            </html>
            """;
    }

    private static object CreateManifest(TheoryDocument document)
    {
        return new
        {
            schemaVersion = document.SchemaVersion,
            builderVersion = document.BuilderVersion,
            documentType = TheoryDocument.DocumentType,
            documentId = document.DocumentId,
            createdAt = document.CreatedAt,
            updatedAt = document.UpdatedAt,
            documentTitle = document.DocumentTitle,
            target = new
            {
                folderPath = document.Target.FolderPath,
                fileName = document.Target.FileName,
                relativePath = document.Target.DisplayPath
            },
            brand = document.Brand,
            runtime = document.Runtime,
            sectionMap = document.SectionMapCells.Select(cell => new
            {
                cell.Id,
                cell.Title,
                cell.AnchorSectionId
            }),
            sections = document.Sections.Select(section => new
            {
                section.Id,
                section.Title
            })
        };
    }

    private static string RenderMapCells(TheoryDocument document)
    {
        var sectionIds = document.Sections.Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
        return string.Join(Environment.NewLine, document.SectionMapCells.Select(cell =>
        {
            var anchorId = cell.AnchorSectionId is { } id && sectionIds.Contains(id)
                ? id
                : "section-map";
            return $"          <a href=\"#{Attribute(anchorId)}\" data-map-cell-id=\"{Attribute(cell.Id)}\">{Text(cell.Title)}</a>";
        }));
    }

    private static string RenderOutlineItems(TheoryDocument document)
    {
        return string.Join(Environment.NewLine, document.Sections.Select((section, index) =>
        {
            var currentClass = index == 0 ? " is-current" : string.Empty;
            var currentAttribute = index == 0 ? " aria-current=\"true\"" : string.Empty;

            return $$"""
                    <a class="builder-outline-item{{currentClass}}" href="#{{Attribute(section.Id)}}"{{currentAttribute}}>
                      <span>{{(index + 1).ToString("00")}}</span>
                      <strong>{{Text(section.Title)}}</strong>
                    </a>
            """;
        }));
    }

    private static string RenderSections(TheoryDocument document)
    {
        return string.Join(Environment.NewLine, document.Sections.Select((section, index) =>
            $$"""
                      <section id="{{Attribute(section.Id)}}" class="builder-section" data-section-id="{{Attribute(section.Id)}}">
                        <div class="builder-section-heading section-heading">
                          <span>{{(index + 1).ToString("00")}}</span>
                          <h3>{{Text(section.Title)}}</h3>
                        </div>
                      </section>
            """));
    }

    private static string Text(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    private static string Attribute(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }

    private const string StudentScript = """
        (() => {
          const appShell = document.querySelector("#appShell");
          const sidebarToggle = document.querySelector("#sidebarToggle");
          const innerToggle = document.querySelector("#innerToggle");

          function setSidebar(open) {
            appShell.classList.toggle("is-sidebar-collapsed", !open);
            sidebarToggle.setAttribute("aria-expanded", String(open));
          }

          function toggleSidebar() {
            setSidebar(appShell.classList.contains("is-sidebar-collapsed"));
          }

          sidebarToggle?.addEventListener("click", toggleSidebar);
          innerToggle?.addEventListener("click", toggleSidebar);
        })();
        """;

    private const string StudentCss = """
        :root {
          color-scheme: dark;
          font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
          --builder-shadow-default: 20px 20px 10px rgba(0, 0, 0, 0.24);
        }

        * {
          box-sizing: border-box;
        }

        html {
          scroll-behavior: smooth;
        }

        body {
          min-width: 320px;
          margin: 0;
        }

        .builder-workspace {
          --builder-bg: #111318;
          --builder-surface: #191d24;
          --builder-surface-strong: #202630;
          --builder-text: #f5f1e8;
          --builder-muted: #c2c5c9;
          --builder-line: #3b414c;
          --builder-accent: #4cc9f0;
          --builder-sidebar-width: 292px;
          --builder-header-height: 84px;
          display: grid;
          min-height: 100vh;
          grid-template-columns: var(--builder-sidebar-width) minmax(0, 1fr);
          grid-template-rows: var(--builder-header-height) minmax(0, 1fr);
          background: linear-gradient(135deg, color-mix(in srgb, #111318 88%, white), #111318);
          color: var(--builder-text);
          transition: grid-template-columns 180ms ease;
        }

        .builder-workspace button,
        .builder-workspace a {
          font: inherit;
        }

        .builder-workspace a {
          color: inherit;
          text-decoration: none;
        }

        .builder-workspace svg {
          width: 20px;
          height: 20px;
          fill: none;
          stroke: currentColor;
          stroke-linecap: round;
          stroke-linejoin: round;
          stroke-width: 2;
        }

        .builder-workspace.is-sidebar-collapsed {
          grid-template-columns: 0 minmax(0, 1fr);
        }

        .builder-topbar {
          position: sticky;
          z-index: 20;
          top: 0;
          display: flex;
          min-width: 0;
          grid-column: 1 / -1;
          align-items: center;
          gap: 18px;
          min-height: var(--builder-header-height);
          padding: 34px;
          border-bottom: 1px solid var(--builder-line);
          background: color-mix(in srgb, var(--builder-surface) 88%, transparent);
          backdrop-filter: blur(18px);
        }

        .builder-topic-title {
          min-width: 0;
          flex: 1 1 auto;
        }

        .builder-topic-title span,
        .builder-section-map span {
          display: block;
          color: var(--builder-accent);
          font-size: 0.75rem;
          font-weight: 800;
          letter-spacing: 0;
          text-transform: uppercase;
        }

        .builder-topic-title h1,
        .builder-section-map h3,
        .builder-section-heading h3 {
          margin: 0;
          overflow-wrap: anywhere;
          line-height: 1.16;
        }

        .builder-topic-title h1 {
          overflow: hidden;
          font-size: clamp(1.32rem, 2.4vw, 2.35rem);
          line-height: 1.28;
          text-overflow: ellipsis;
          white-space: nowrap;
        }

        .builder-icon-button,
        .builder-panel-button {
          display: inline-grid;
          place-items: center;
          border: 1px solid var(--builder-line);
          border-radius: 8px;
          background: var(--builder-surface-strong);
          color: var(--builder-text);
          cursor: pointer;
          transition:
            border-color 160ms ease,
            background 160ms ease,
            color 160ms ease,
            transform 160ms ease;
        }

        .builder-icon-button {
          width: 44px;
          height: 44px;
          flex: 0 0 44px;
        }

        .builder-panel-button {
          width: 34px;
          height: 34px;
          flex: 0 0 34px;
        }

        .builder-icon-button:hover,
        .builder-panel-button:hover {
          border-color: var(--builder-accent);
          background: color-mix(in srgb, var(--builder-accent) 12%, var(--builder-surface-strong));
          transform: translateY(-1px);
        }

        .builder-icon-button:focus-visible,
        .builder-panel-button:focus-visible,
        .builder-outline-item:focus-visible,
        .builder-section-map-grid a:focus-visible {
          outline: 2px solid color-mix(in srgb, var(--builder-accent) 70%, transparent);
          outline-offset: 2px;
        }

        .builder-sidebar {
          position: sticky;
          top: var(--builder-header-height);
          display: flex;
          flex-direction: column;
          overflow: hidden;
          height: calc(100vh - var(--builder-header-height));
          min-width: 0;
          padding: 22px 18px;
          border-right: 1px solid var(--builder-line);
          background: color-mix(in srgb, var(--builder-surface) 92%, transparent);
          transition:
            opacity 160ms ease,
            transform 180ms ease,
            visibility 160ms ease;
        }

        .is-sidebar-collapsed .builder-sidebar {
          visibility: hidden;
          opacity: 0;
          pointer-events: none;
        }

        .builder-sidebar-head {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 12px;
          margin-bottom: 16px;
          color: var(--builder-muted);
          font-size: 0.82rem;
          font-weight: 850;
          text-transform: uppercase;
        }

        .builder-outline {
          display: grid;
          align-content: start;
          flex: 1 1 auto;
          gap: 8px;
          min-height: 0;
          overflow: hidden auto;
        }

        .builder-sidebar-footer {
          flex: 0 0 auto;
          margin-top: 18px;
          padding-top: 14px;
          border-top: 1px solid var(--builder-line);
          color: var(--builder-muted);
          font-size: 0.78rem;
          font-weight: 700;
        }

        button.builder-outline-item,
        a.builder-outline-item {
          display: grid;
          grid-template-columns: 38px minmax(0, 1fr);
          align-items: center;
          width: 100%;
          min-height: 46px;
          padding: 8px 10px;
          border: 1px solid transparent;
          border-radius: 8px;
          background: transparent;
          color: var(--builder-muted);
          cursor: pointer;
          text-align: left;
          text-decoration: none;
        }

        button.builder-outline-item span,
        a.builder-outline-item span {
          color: var(--builder-accent);
          font-size: 0.78rem;
          font-weight: 900;
        }

        button.builder-outline-item strong,
        a.builder-outline-item strong {
          min-width: 0;
          overflow: hidden;
          font-size: 0.9rem;
          font-weight: 750;
          text-overflow: ellipsis;
          white-space: nowrap;
        }

        button.builder-outline-item:hover,
        button.builder-outline-item.is-current,
        a.builder-outline-item:hover,
        a.builder-outline-item.is-current {
          border-color: var(--builder-line);
          background: var(--builder-surface-strong);
          color: var(--builder-text);
        }

        .builder-main {
          display: grid;
          align-content: start;
          gap: 38px;
          box-sizing: border-box;
          width: 100%;
          max-width: 1250px;
          min-width: 0;
          margin-inline: auto;
          padding: 56px;
        }

        .builder-section-map {
          display: grid;
          grid-template-columns: minmax(0, 0.78fr) minmax(0, 1.22fr);
          gap: 28px;
          align-items: end;
          padding-bottom: 30px;
          border-bottom: 1px solid var(--builder-line);
          scroll-margin-top: calc(var(--builder-header-height) + 24px);
        }

        .builder-section-map h3 {
          font-size: clamp(1.7rem, 3vw, 3.25rem);
        }

        .builder-section-map-grid {
          display: grid;
          grid-template-columns: repeat(2, minmax(0, 1fr));
          grid-template-rows: repeat(3, minmax(56px, auto));
          gap: 12px;
        }

        .builder-section-map-grid a {
          min-width: 0;
          min-height: 56px;
          padding: 16px;
          border: 1px solid #3b414c;
          border-radius: 8px;
          background: #191d24;
          box-shadow: 20px 10px 10px rgba(0, 0, 0, 0.24);
          color: var(--builder-text);
          cursor: pointer;
          overflow-wrap: break-word;
          text-align: left;
          font-weight: 800;
          transition:
            border-color 160ms ease,
            background 160ms ease;
        }

        .builder-section-map-grid a:hover {
          border-color: var(--builder-accent);
          background: color-mix(in srgb, var(--builder-accent) 16%, #191d24);
        }

        .builder-section-skeleton {
          display: grid;
          min-width: 0;
          gap: 50px;
        }

        .builder-section {
          min-width: 0;
          padding: clamp(18px, 3vw, 32px);
          border: 1px solid var(--builder-line);
          border-radius: 8px;
          background: color-mix(in srgb, var(--builder-surface-strong) 94%, transparent);
          box-shadow: var(--builder-shadow-default);
          scroll-margin-top: calc(var(--builder-header-height) + 24px);
        }

        .builder-section-heading {
          display: flex;
          align-items: baseline;
          gap: 12px;
          margin-bottom: 4px;
        }

        .builder-section-heading span {
          color: var(--builder-accent);
          font-size: 0.86rem;
          font-weight: 900;
        }

        .builder-section-heading h3 {
          font-size: 1.32rem;
        }

        @media (max-width: 980px) {
          .builder-section-map {
            display: grid;
            grid-template-columns: 1fr;
            align-items: stretch;
          }
        }

        @media (max-width: 760px) {
          .builder-workspace,
          .builder-workspace.is-sidebar-collapsed {
            grid-template-columns: minmax(0, 1fr);
          }

          .builder-sidebar {
            position: fixed;
            z-index: 18;
            top: var(--builder-header-height);
            left: 0;
            width: min(var(--builder-sidebar-width), calc(100vw - 32px));
            height: calc(100dvh - var(--builder-header-height));
            box-shadow: 0 20px 52px rgba(0, 0, 0, 0.32);
            transform: translateX(0);
          }

          .is-sidebar-collapsed .builder-sidebar {
            transform: translateX(-100%);
          }
        }

        """;
}
