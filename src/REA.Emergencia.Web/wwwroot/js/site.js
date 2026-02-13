function createDefaultWorkflow() {
  return {
    id: "",
    name: "",
    version: "1.0.0",
    initialState: "",
    states: []
  };
}

function normalizeWorkflow(data) {
  const normalized = createDefaultWorkflow();
  if (!data || typeof data !== "object") {
    return normalized;
  }

  normalized.id = typeof data.id === "string" ? data.id : "";
  normalized.name = typeof data.name === "string" ? data.name : "";
  normalized.version = typeof data.version === "string" ? data.version : "1.0.0";
  normalized.initialState = typeof data.initialState === "string" ? data.initialState : "";

  if (Array.isArray(data.states)) {
    normalized.states = data.states.map((state) => {
      const transitions = Array.isArray(state.transitions) ? state.transitions : [];
      return {
        key: typeof state.key === "string" ? state.key : "",
        label: typeof state.label === "string" ? state.label : "",
        type: state.type === "terminal" ? "terminal" : "normal",
        transitions: transitions.map((transition) => ({
          to: typeof transition.to === "string" ? transition.to : "",
          event: typeof transition.event === "string" ? transition.event : ""
        }))
      };
    });
  }

  return normalized;
}

function setupWorkflowBuilder(builder) {
  const textareaId = builder.dataset.workflowTarget;
  if (!textareaId) {
    return;
  }

  const workflowTextarea = document.getElementById(textareaId);
  if (!workflowTextarea) {
    return;
  }

  const statesContainer = builder.querySelector("[data-wf-states]");
  const form = builder.closest("form");
  const mermaidTarget = (form ? form.querySelector("[data-wf-mermaid]") : null) || document.querySelector("[data-wf-mermaid]");
  const initialStateSelect = builder.querySelector("[data-wf-initial-state]");
  const idInput = builder.querySelector('[data-wf-field="id"]');
  const nameInput = builder.querySelector('[data-wf-field="name"]');
  const versionInput = builder.querySelector('[data-wf-field="version"]');
  const addStateButton = builder.querySelector("[data-wf-add-state]");
  const loadJsonButton = builder.querySelector("[data-wf-load-json]");

  if (!statesContainer || !initialStateSelect || !idInput || !nameInput || !versionInput || !addStateButton || !loadJsonButton) {
    return;
  }

  let workflow = createDefaultWorkflow();

  function syncJsonField() {
    workflowTextarea.value = JSON.stringify(workflow, null, 2);
  }

  function toMermaidNodeId(key, index) {
    const clean = (key || "").replace(/[^a-zA-Z0-9_]/g, "_");
    return clean.length > 0 ? `S_${clean}` : `S_IDX_${index}`;
  }

  function sanitizeMermaidText(value) {
    return (value || "")
      .replace(/"/g, "")
      .replace(/\[/g, "(")
      .replace(/\]/g, ")")
      .replace(/\{/g, "(")
      .replace(/\}/g, ")")
      .replace(/\|/g, "/")
      .replace(/\n/g, " ")
      .trim();
  }

  function buildMermaidText() {
    if (workflow.states.length === 0) {
      return "flowchart LR\n  Empty[Sem estados]";
    }

    const lines = [
      "flowchart LR",
      "  Start([Start])",
      "  classDef terminal fill:#fde2e1,stroke:#d9534f,color:#111;"
    ];
    const nodeIds = workflow.states.map((state, index) => toMermaidNodeId(state.key, index));

    workflow.states.forEach((state, index) => {
      const labelBase = state.label && state.label.trim().length > 0 ? state.label : state.key || `State ${index + 1}`;
      const nodeLabel = sanitizeMermaidText(`${labelBase} (${state.key || "sem_key"})`);
      lines.push(`  ${nodeIds[index]}["${nodeLabel}"]`);
      if (state.type === "terminal") {
        lines.push(`  class ${nodeIds[index]} terminal;`);
      }
    });

    workflow.states.forEach((state, index) => {
      const fromId = nodeIds[index];
      (state.transitions || []).forEach((transition) => {
        if (!transition.to) {
          return;
        }
        const targetIndex = workflow.states.findIndex((x) => x.key === transition.to);
        if (targetIndex < 0) {
          return;
        }
        const toId = nodeIds[targetIndex];
        const eventLabel = transition.event ? `|${sanitizeMermaidText(transition.event)}|` : "";
        lines.push(`  ${fromId} -->${eventLabel} ${toId}`);
      });
    });

    if (workflow.initialState) {
      const initialIndex = workflow.states.findIndex((x) => x.key === workflow.initialState);
      if (initialIndex >= 0) {
        lines.push(`  Start --> ${nodeIds[initialIndex]}`);
      }
    }

    return lines.join("\n");
  }

  async function renderMermaid() {
    if (!mermaidTarget) {
      return;
    }

    const chart = buildMermaidText();
    mermaidTarget.textContent = chart;

    if (!window.mermaid || typeof window.mermaid.render !== "function") {
      return;
    }

    if (!window.__workflowMermaidInitialized) {
      window.mermaid.initialize({ startOnLoad: false, securityLevel: "loose" });
      window.__workflowMermaidInitialized = true;
    }

    try {
      const renderId = `wf_mermaid_${Date.now()}_${Math.floor(Math.random() * 10000)}`;
      const result = await window.mermaid.render(renderId, chart);
      mermaidTarget.innerHTML = result.svg;
    } catch (error) {
      console.error("Mermaid render error", error);
      mermaidTarget.textContent = chart;
    }
  }

  function stateKeys() {
    return workflow.states
      .map((state) => state.key)
      .filter((key) => key && key.trim().length > 0);
  }

  function ensureInitialStateIsValid() {
    const keys = stateKeys();
    if (!keys.includes(workflow.initialState)) {
      workflow.initialState = keys.length > 0 ? keys[0] : "";
    }
  }

  function renderInitialStateOptions() {
    const keys = stateKeys();
    initialStateSelect.innerHTML = "";

    const emptyOption = document.createElement("option");
    emptyOption.value = "";
    emptyOption.textContent = keys.length > 0 ? "Selecione..." : "Sem estados";
    initialStateSelect.appendChild(emptyOption);

    keys.forEach((key) => {
      const option = document.createElement("option");
      option.value = key;
      option.textContent = key;
      initialStateSelect.appendChild(option);
    });

    initialStateSelect.value = workflow.initialState || "";
  }

  function createTransitionRow(stateIndex, transitionIndex) {
    const transition = workflow.states[stateIndex].transitions[transitionIndex];
    const row = document.createElement("div");
    row.className = "row g-2 mb-2";

    const toCol = document.createElement("div");
    toCol.className = "col-md-5";
    const toSelect = document.createElement("select");
    toSelect.className = "form-select";
    const empty = document.createElement("option");
    empty.value = "";
    empty.textContent = "Destino";
    toSelect.appendChild(empty);

    const keys = stateKeys();
    keys.forEach((key) => {
      const option = document.createElement("option");
      option.value = key;
      option.textContent = key;
      toSelect.appendChild(option);
    });

    if (transition.to && !keys.includes(transition.to)) {
      const legacy = document.createElement("option");
      legacy.value = transition.to;
      legacy.textContent = transition.to;
      toSelect.appendChild(legacy);
    }

    toSelect.value = transition.to || "";
    toSelect.addEventListener("change", () => {
      workflow.states[stateIndex].transitions[transitionIndex].to = toSelect.value;
      syncJsonField();
    });
    toCol.appendChild(toSelect);

    const eventCol = document.createElement("div");
    eventCol.className = "col-md-5";
    const eventInput = document.createElement("input");
    eventInput.className = "form-control";
    eventInput.placeholder = "Evento";
    eventInput.value = transition.event || "";
    eventInput.addEventListener("input", () => {
      workflow.states[stateIndex].transitions[transitionIndex].event = eventInput.value;
      syncJsonField();
    });
    eventCol.appendChild(eventInput);

    const removeCol = document.createElement("div");
    removeCol.className = "col-md-2";
    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.className = "btn btn-outline-danger w-100";
    removeButton.textContent = "Remover";
    removeButton.addEventListener("click", () => {
      workflow.states[stateIndex].transitions.splice(transitionIndex, 1);
      render();
    });
    removeCol.appendChild(removeButton);

    row.appendChild(toCol);
    row.appendChild(eventCol);
    row.appendChild(removeCol);
    return row;
  }

  function createStateCard(state, stateIndex) {
    const card = document.createElement("div");
    card.className = "card mb-3 workflow-state-card";

    const body = document.createElement("div");
    body.className = "card-body";

    const header = document.createElement("div");
    header.className = "d-flex justify-content-between align-items-center mb-3";
    const title = document.createElement("h3");
    title.className = "h6 mb-0";
    title.textContent = `Estado #${stateIndex + 1}`;
    const removeState = document.createElement("button");
    removeState.type = "button";
    removeState.className = "btn btn-sm btn-outline-danger";
    removeState.textContent = "Eliminar Estado";
    removeState.addEventListener("click", () => {
      workflow.states.splice(stateIndex, 1);
      ensureInitialStateIsValid();
      render();
    });
    header.appendChild(title);
    header.appendChild(removeState);

    const row = document.createElement("div");
    row.className = "row g-3 mb-3";

    const keyCol = document.createElement("div");
    keyCol.className = "col-md-4";
    const keyInput = document.createElement("input");
    keyInput.className = "form-control";
    keyInput.placeholder = "Key";
    keyInput.value = state.key;
    keyInput.addEventListener("input", () => {
      state.key = keyInput.value;
      ensureInitialStateIsValid();
      renderInitialStateOptions();
      syncJsonField();
    });
    keyCol.appendChild(keyInput);

    const labelCol = document.createElement("div");
    labelCol.className = "col-md-4";
    const labelInput = document.createElement("input");
    labelInput.className = "form-control";
    labelInput.placeholder = "Label";
    labelInput.value = state.label;
    labelInput.addEventListener("input", () => {
      state.label = labelInput.value;
      syncJsonField();
    });
    labelCol.appendChild(labelInput);

    const typeCol = document.createElement("div");
    typeCol.className = "col-md-4";
    const typeSelect = document.createElement("select");
    typeSelect.className = "form-select";
    const normalOption = document.createElement("option");
    normalOption.value = "normal";
    normalOption.textContent = "normal";
    const terminalOption = document.createElement("option");
    terminalOption.value = "terminal";
    terminalOption.textContent = "terminal";
    typeSelect.appendChild(normalOption);
    typeSelect.appendChild(terminalOption);
    typeSelect.value = state.type;
    typeSelect.addEventListener("change", () => {
      state.type = typeSelect.value;
      if (state.type === "terminal") {
        state.transitions = [];
      }
      render();
    });
    typeCol.appendChild(typeSelect);

    row.appendChild(keyCol);
    row.appendChild(labelCol);
    row.appendChild(typeCol);

    const transitionsTitle = document.createElement("h4");
    transitionsTitle.className = "h6";
    transitionsTitle.textContent = "Transitions";

    const transitionsContainer = document.createElement("div");
    transitionsContainer.className = "workflow-transitions";

    if (state.transitions.length === 0) {
      const empty = document.createElement("div");
      empty.className = "text-muted small mb-2";
      empty.textContent = "Sem transitions.";
      transitionsContainer.appendChild(empty);
    } else {
      state.transitions.forEach((_, transitionIndex) => {
        transitionsContainer.appendChild(createTransitionRow(stateIndex, transitionIndex));
      });
    }

    const addTransitionButton = document.createElement("button");
    addTransitionButton.type = "button";
    addTransitionButton.className = "btn btn-sm btn-outline-primary";
    addTransitionButton.textContent = "Adicionar Transition";
    addTransitionButton.disabled = state.type === "terminal";
    addTransitionButton.addEventListener("click", () => {
      state.transitions.push({
        to: "",
        event: ""
      });
      render();
    });

    body.appendChild(header);
    body.appendChild(row);
    body.appendChild(transitionsTitle);
    body.appendChild(transitionsContainer);
    body.appendChild(addTransitionButton);
    card.appendChild(body);
    return card;
  }

  function render() {
    idInput.value = workflow.id || "";
    nameInput.value = workflow.name || "";
    versionInput.value = workflow.version || "";

    ensureInitialStateIsValid();
    renderInitialStateOptions();

    statesContainer.innerHTML = "";
    workflow.states.forEach((state, stateIndex) => {
      statesContainer.appendChild(createStateCard(state, stateIndex));
    });

    syncJsonField();
    renderMermaid();
  }

  function loadFromJsonField() {
    try {
      const parsed = workflowTextarea.value && workflowTextarea.value.trim()
        ? JSON.parse(workflowTextarea.value)
        : createDefaultWorkflow();
      workflow = normalizeWorkflow(parsed);
      ensureInitialStateIsValid();
      render();
    } catch {
      alert("JSON invalido. Corrija o conteudo e tente novamente.");
    }
  }

  idInput.addEventListener("input", () => {
    workflow.id = idInput.value;
    syncJsonField();
  });

  nameInput.addEventListener("input", () => {
    workflow.name = nameInput.value;
    syncJsonField();
  });

  versionInput.addEventListener("input", () => {
    workflow.version = versionInput.value;
    syncJsonField();
  });

  initialStateSelect.addEventListener("change", () => {
    workflow.initialState = initialStateSelect.value;
    syncJsonField();
  });

  addStateButton.addEventListener("click", () => {
    workflow.states.push({
      key: "",
      label: "",
      type: "normal",
      transitions: []
    });
    ensureInitialStateIsValid();
    render();
  });

  loadJsonButton.addEventListener("click", loadFromJsonField);

  loadFromJsonField();
}

document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-workflow-builder]").forEach(setupWorkflowBuilder);
});
