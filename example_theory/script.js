const appShell = document.querySelector("#appShell");
const sidebarToggle = document.querySelector("#sidebarToggle");
const innerToggle = document.querySelector("#innerToggle");
const themeToggle = document.querySelector("#themeToggle");
const tocItems = document.querySelectorAll(".toc-item");
const lessonSections = document.querySelectorAll(".lesson-section");
const quizOptions = document.querySelectorAll(".quiz-option");
const quizInputForms = document.querySelectorAll(".quiz-input-form");
const codeLabs = document.querySelectorAll("[data-code-lab]");
const codeViewers = document.querySelectorAll("[data-code-viewer]");
const mobileSidebarQuery = window.matchMedia("(max-width: 820px)");
const sectionById = new Map(Array.from(lessonSections, (section) => [section.id, section]));
const copyIconSvg = `
  <svg viewBox="0 0 24 24" aria-hidden="true">
    <path d="M8 7a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-7a2 2 0 0 1-2-2V7Z" />
    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v1" />
  </svg>
`;
const checkIconSvg = `
  <svg viewBox="0 0 24 24" aria-hidden="true">
    <path d="m5 12 5 5L20 7" />
  </svg>
`;
const resetIconSvg = `
  <svg viewBox="0 0 24 24" aria-hidden="true">
    <path d="M5.2 8.1a8 8 0 1 1-.5 7.3" />
    <path d="M5 4v4h4" />
  </svg>
`;
let scrollTicking = false;
let pyodideReadyPromise;
let monacoReadyPromise;
const monacoEditors = new Set();

function setSidebar(open) {
  appShell.classList.toggle("sidebar-collapsed", !open);
  sidebarToggle.setAttribute("aria-expanded", String(open));
  sidebarToggle.setAttribute("aria-label", open ? "Скрыть левую панель" : "Открыть левую панель");
}

function toggleSidebar() {
  setSidebar(appShell.classList.contains("sidebar-collapsed"));
}

function setCurrentTocItem(sectionId) {
  tocItems.forEach((item) => {
    const isCurrent = item.getAttribute("href") === `#${sectionId}`;

    item.classList.toggle("current", isCurrent);
    if (isCurrent) {
      item.setAttribute("aria-current", "location");
    } else {
      item.removeAttribute("aria-current");
    }
  });
}

function getCurrentSectionId() {
  const headerOffset = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--header-height")) || 0;
  let currentSectionId = lessonSections[0]?.id;
  let bestVisibleRatio = 0;

  lessonSections.forEach((section) => {
    const rect = section.getBoundingClientRect();
    const visibleHeight = Math.min(rect.bottom, window.innerHeight) - Math.max(rect.top, headerOffset);
    const visibleRatio = Math.max(0, visibleHeight) / rect.height;

    if (visibleRatio >= bestVisibleRatio) {
      bestVisibleRatio = visibleRatio;
      currentSectionId = section.id;
    }
  });

  return currentSectionId;
}

function updateCurrentTocItem() {
  setCurrentTocItem(getCurrentSectionId());
  scrollTicking = false;
}

function requestCurrentTocUpdate() {
  if (scrollTicking) {
    return;
  }

  scrollTicking = true;
  window.requestAnimationFrame(updateCurrentTocItem);
}

sidebarToggle.addEventListener("click", toggleSidebar);
innerToggle.addEventListener("click", toggleSidebar);

tocItems.forEach((item) => {
  item.addEventListener("click", () => {
    const sectionId = item.getAttribute("href").slice(1);

    if (sectionById.has(sectionId)) {
      setCurrentTocItem(sectionId);
    }

    if (mobileSidebarQuery.matches) {
      setSidebar(false);
    }
  });
});

window.addEventListener("scroll", requestCurrentTocUpdate, { passive: true });
window.addEventListener("resize", requestCurrentTocUpdate);
setCurrentTocItem(getCurrentSectionId());

function setLightTheme(enabled) {
  document.body.classList.toggle("theme-light", enabled);
  themeToggle.setAttribute("aria-pressed", String(enabled));
  themeToggle.setAttribute(
    "aria-label",
    enabled ? "Включить темный вариант" : "Включить светлый вариант",
  );
}

themeToggle.addEventListener("click", () => {
  setLightTheme(!document.body.classList.contains("theme-light"));
  updateEditorThemes();
});

quizOptions.forEach((option) => {
  option.addEventListener("click", () => {
    const quizBlock = option.closest(".quiz-block");
    const blockOptions = quizBlock.querySelectorAll(".quiz-option");
    const isMultiple = quizBlock.dataset.quizMode === "multiple";
    const isCorrect = option.dataset.correct === "true";

    if (!isMultiple) {
      blockOptions.forEach((item) => {
        item.classList.remove("is-correct", "is-incorrect");
        item.setAttribute("aria-pressed", "false");

        const itemFeedback = item.closest(".quiz-choice").querySelector(".quiz-feedback");
        itemFeedback.textContent = "";
        itemFeedback.className = "quiz-feedback";
      });
    } else if (option.getAttribute("aria-pressed") === "true") {
      const feedback = option.closest(".quiz-choice").querySelector(".quiz-feedback");

      option.classList.remove("is-correct", "is-incorrect");
      option.setAttribute("aria-pressed", "false");
      feedback.textContent = "";
      feedback.className = "quiz-feedback";
      return;
    }

    const feedback = option.closest(".quiz-choice").querySelector(".quiz-feedback");

    option.classList.add(isCorrect ? "is-correct" : "is-incorrect");
    option.setAttribute("aria-pressed", "true");
    feedback.textContent = option.dataset.feedback;
    feedback.className = `quiz-feedback is-visible ${isCorrect ? "is-correct" : "is-incorrect"}`;
  });
});

quizInputForms.forEach((form) => {
  form.addEventListener("submit", (event) => {
    event.preventDefault();

    const input = form.querySelector(".quiz-input");
    const feedback = form.querySelector(".quiz-feedback");
    const acceptedAnswers = form.dataset.answer.split("|").map((answer) => answer.trim().toLowerCase());
    const userAnswer = input.value.trim();
    const isCorrect = acceptedAnswers.includes(userAnswer);

    feedback.textContent = isCorrect
      ? feedback.dataset.correctFeedback
      : feedback.dataset.incorrectFeedback;
    feedback.className = `quiz-feedback is-visible ${isCorrect ? "is-correct" : "is-incorrect"}`;
  });
});

function getEditorTheme() {
  return "vs-dark";
}

function updateEditorThemes() {
  if (!window.monaco) {
    return;
  }

  window.monaco.editor.setTheme(getEditorTheme());
}

function relayoutMonacoEditors() {
  monacoEditors.forEach((editor) => {
    editor.layout();
  });
}

function getMonaco() {
  if (monacoReadyPromise) {
    return monacoReadyPromise;
  }

  monacoReadyPromise = new Promise((resolve, reject) => {
    if (window.monaco) {
      resolve(window.monaco);
      return;
    }

    if (!window.require) {
      reject(new Error("Monaco loader is not available"));
      return;
    }

    window.require.config({
      paths: {
        vs: "https://cdn.jsdelivr.net/npm/monaco-editor@0.49.0/min/vs",
      },
    });
    window.require(["vs/editor/editor.main"], () => resolve(window.monaco), reject);
  });

  return monacoReadyPromise;
}

function getPyodide() {
  if (pyodideReadyPromise) {
    return pyodideReadyPromise;
  }

  pyodideReadyPromise = window.loadPyodide({
    indexURL: "https://cdn.jsdelivr.net/pyodide/v0.26.4/full/",
  });

  return pyodideReadyPromise;
}

function appendOutput(output, text) {
  output.append(document.createTextNode(text));
  output.scrollTop = output.scrollHeight;
}

function requestCodeInput(output, promptText) {
  return new Promise((resolve) => {
    output.classList.add("is-waiting");
    appendOutput(output, promptText || "");

    const form = document.createElement("form");
    form.className = "code-input-form";
    form.innerHTML = `
      <div class="code-input-row">
        <input class="code-input" type="text" autocomplete="off" />
        <button class="code-input-submit" type="submit">Ввести</button>
      </div>
    `;

    const input = form.querySelector(".code-input");
    output.append(form);
    input.focus();
    output.scrollTop = output.scrollHeight;

    form.addEventListener("submit", (event) => {
      event.preventDefault();
      const value = input.value;

      form.remove();
      output.classList.remove("is-waiting");
      appendOutput(output, `${value}\n`);
      resolve(value);
    });
  });
}

function prepareInteractiveCode(code) {
  return code.replace(/\binput\s*\(/g, "await input(");
}

function hasInputInsideBlock(code) {
  return code.split("\n").some((line) => /^\s+\S/.test(line) && /\binput\s*\(/.test(line));
}

function getInputPrompts(code) {
  const prompts = [];
  const inputPattern = /\binput\s*\(\s*(?:"([^"]*)"|'([^']*)')?/g;
  let match = inputPattern.exec(code);

  while (match) {
    prompts.push(match[1] || match[2] || "Введите значение: ");
    match = inputPattern.exec(code);
  }

  return prompts;
}

async function collectQueuedInput(output, code) {
  const prompts = getInputPrompts(code);
  const values = [];

  for (const promptText of prompts) {
    values.push(await requestCodeInput(output, promptText));
  }

  return values;
}

function getCodeLabCode(lab) {
  const editor = lab.editor;
  const source = lab.querySelector(".code-source");

  return editor ? editor.getValue() : source.value;
}

function getCopyButtonCode(copyButton) {
  const frame = copyButton.closest(".code-editor-frame");
  const editorContainer = frame?.querySelector(".code-editor");
  const source =
    frame?.querySelector(".code-source") ||
    (frame?.nextElementSibling?.matches(".code-source")
      ? frame.nextElementSibling
      : copyButton.closest("[data-code-lab]")?.querySelector(".code-source"));

  if (editorContainer?.editor) {
    return editorContainer.editor.getValue();
  }

  return source?.value || "";
}

function setRunButtonBusy(runButton, isBusy) {
  runButton.disabled = isBusy;
  runButton.setAttribute("aria-busy", String(isBusy));
}

function getPresetCode(source) {
  return source?.dataset.initialCode ?? source?.value ?? "";
}

function isEditableCodeLab(lab) {
  return lab.querySelector(".code-editor")?.dataset.readonly !== "true";
}

function addResetCodeButton(lab) {
  if (!isEditableCodeLab(lab)) {
    return;
  }

  const actions = lab.querySelector(".code-lab-actions");

  if (!actions || actions.querySelector(".reset-code-button")) {
    return;
  }

  const resetButton = document.createElement("button");

  resetButton.type = "button";
  resetButton.className = "reset-code-button";
  resetButton.setAttribute("aria-label", "Сбросить код");
  resetButton.innerHTML = resetIconSvg;
  actions.prepend(resetButton);
  resetButton.addEventListener("click", () => resetCodeLab(lab));
}

function resetCodeLab(lab) {
  const source = lab.querySelector(".code-source");
  const presetCode = getPresetCode(source);
  const editorContainer = lab.querySelector(".code-editor");

  if (editorContainer?.editor) {
    editorContainer.editor.setValue(presetCode);
    editorContainer.editor.focus();
    return;
  }

  if (source) {
    source.value = presetCode;
    source.dispatchEvent(new Event("input", { bubbles: true }));
    source.focus();
  }
}

function setCopyButtonCopied(copyButton) {
  const originalLabel = copyButton.getAttribute("aria-label");

  copyButton.setAttribute("aria-label", "Код скопирован");
  copyButton.innerHTML = checkIconSvg;
  copyButton.classList.add("is-copied");

  window.setTimeout(() => {
    copyButton.setAttribute("aria-label", originalLabel || "Скопировать код");
    copyButton.innerHTML = copyIconSvg;
    copyButton.classList.remove("is-copied");
  }, 1200);
}

async function copyCode(copyButton) {
  const code = getCopyButtonCode(copyButton);

  try {
    await navigator.clipboard.writeText(code);
    setCopyButtonCopied(copyButton);
  } catch {
    const copyArea = document.createElement("textarea");

    copyArea.value = code;
    copyArea.setAttribute("readonly", "");
    copyArea.style.position = "fixed";
    copyArea.style.inset = "0 auto auto 0";
    copyArea.style.opacity = "0";
    document.body.append(copyArea);
    copyArea.select();
    document.execCommand("copy");
    copyArea.remove();
    setCopyButtonCopied(copyButton);
  }
}

async function runCodeLab(lab) {
  const runButton = lab.querySelector(".run-button");
  const output = lab.querySelector(".code-output");
  const code = getCodeLabCode(lab);

  output.className = "code-output";
  output.textContent = "";
  setRunButtonBusy(runButton, true);

  try {
    if (!window.loadPyodide) {
      throw new Error("Pyodide is not loaded");
    }

    const pyodide = await getPyodide();

    pyodide.setStdout({
      batched: (text) => appendOutput(output, `${text}\n`),
    });
    pyodide.setStderr({
      batched: (text) => appendOutput(output, `${text}\n`),
    });
    window.lessonRequestInput = (promptText) => requestCodeInput(output, promptText);
    pyodide.setStdin({
      stdin: () => "",
    });

    if (hasInputInsideBlock(code)) {
      const inputValues = await collectQueuedInput(output, code);

      pyodide.setStdin({
        stdin: () => inputValues.shift() || "",
      });
      await pyodide.runPythonAsync(code);
    } else {
      const wrappedCode = `
import builtins
from js import lessonRequestInput

async def input(prompt=""):
    return await lessonRequestInput(prompt)

${prepareInteractiveCode(code)}
`;

      await pyodide.runPythonAsync(wrappedCode);
    }

    if (!output.textContent.trim()) {
      appendOutput(output, "Код выполнен без вывода.\n");
    }
  } catch (error) {
    output.classList.add("is-error");
    appendOutput(output, `${error.message || error}\n`);
  } finally {
    setRunButtonBusy(runButton, false);
  }
}

function initFallbackEditor(lab) {
  const source = lab.querySelector(".code-source");
  const container = lab.querySelector(".code-editor");

  source.style.display = "block";
  source.classList.add("code-editor");
  source.readOnly = container.dataset.readonly === "true";
  autoSizeTextarea(source);
  container.replaceWith(source);
}

function initFallbackCodeViewer(container) {
  const frame = container.closest(".code-editor-frame");
  const source = frame?.nextElementSibling;

  source.style.display = "block";
  source.classList.add("code-editor", "code-viewer");
  source.readOnly = true;
  autoSizeTextarea(source);
  container.replaceWith(source);
}

function autoSizeTextarea(textarea) {
  textarea.style.height = "auto";
  textarea.style.height = `${textarea.scrollHeight + 2}px`;
  textarea.addEventListener("input", () => {
    textarea.style.height = "auto";
    textarea.style.height = `${textarea.scrollHeight + 2}px`;
  });
}

function autoSizeMonacoEditor(editor, container) {
  let resizeFrame = null;

  const updateHeight = () => {
    resizeFrame = null;
    const contentHeight = Math.ceil(editor.getContentHeight());

    container.style.height = `${contentHeight + 2}px`;
    editor.layout({ width: container.clientWidth, height: contentHeight });
  };

  const scheduleUpdate = () => {
    if (resizeFrame !== null) {
      return;
    }

    resizeFrame = window.requestAnimationFrame(updateHeight);
  };

  editor.onDidContentSizeChange(updateHeight);

  if ("ResizeObserver" in window) {
    const resizeObserver = new ResizeObserver(scheduleUpdate);

    resizeObserver.observe(container);
  }

  scheduleUpdate();
}

function createMonacoEditor(monaco, container, source, options = {}) {
  const editor = monaco.editor.create(container, {
    value: source.value,
    language: "python",
    theme: getEditorTheme(),
    automaticLayout: true,
    fontSize: 14,
    minimap: { enabled: false },
    padding: { top: 12, bottom: 12 },
    readOnly: container.dataset.readonly === "true",
    scrollBeyondLastLine: false,
    scrollbar: {
      alwaysConsumeMouseWheel: false,
      handleMouseWheel: false,
    },
    tabSize: 4,
    wordWrap: "on",
    ...options,
  });

  autoSizeMonacoEditor(editor, container);
  monacoEditors.add(editor);
  container.editor = editor;

  return editor;
}

window.addEventListener("resize", () => {
  window.requestAnimationFrame(relayoutMonacoEditors);
});

codeLabs.forEach((lab) => {
  const runButton = lab.querySelector(".run-button");
  const source = lab.querySelector(".code-source");

  if (source) {
    source.dataset.initialCode = source.value;
  }

  runButton.addEventListener("click", () => runCodeLab(lab));
  addResetCodeButton(lab);
});

document.querySelectorAll(".copy-code-button").forEach((copyButton) => {
  copyButton.addEventListener("click", () => copyCode(copyButton));
});

if (codeLabs.length || codeViewers.length) {
  getMonaco()
    .then((monaco) => {
      codeViewers.forEach((container) => {
        const source = container.closest(".code-editor-frame")?.nextElementSibling;

        createMonacoEditor(monaco, container, source, {
          lineNumbersMinChars: 3,
          readOnly: true,
        });
      });

      codeLabs.forEach((lab) => {
        const container = lab.querySelector(".code-editor");
        const source = lab.querySelector(".code-source");

        lab.editor = createMonacoEditor(monaco, container, source);
      });
    })
    .catch(() => {
      codeViewers.forEach(initFallbackCodeViewer);
      codeLabs.forEach(initFallbackEditor);
    });
}
