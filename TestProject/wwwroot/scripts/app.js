import { ApiError, filesApi } from "./api.js";

const elements = {
    breadcrumbs: document.querySelector("#breadcrumbs"),
    clearSearchButton: document.querySelector("#clear-search-button"),
    createFolderButton: document.querySelector("#create-folder-button"),
    createFolderDialog: document.querySelector("#create-folder-dialog"),
    createFolderForm: document.querySelector("#create-folder-form"),
    createParentLabel: document.querySelector("#create-parent-label"),
    deleteDialog: document.querySelector("#delete-dialog"),
    deleteForm: document.querySelector("#delete-form"),
    deleteSourceLabel: document.querySelector("#delete-source-label"),
    destinationDirectory: document.querySelector("#destination-directory"),
    destinationName: document.querySelector("#destination-name"),
    emptyMessage: document.querySelector("#empty-message"),
    entryList: document.querySelector("#entry-list"),
    fileCount: document.querySelector("#file-count"),
    fileLabel: document.querySelector("#file-label"),
    folderCount: document.querySelector("#folder-count"),
    folderLabel: document.querySelector("#folder-label"),
    folderName: document.querySelector("#folder-name"),
    overwriteUpload: document.querySelector("#overwrite-upload"),
    recursiveDelete: document.querySelector("#recursive-delete"),
    recursiveDeleteLabel: document.querySelector("#recursive-delete-label"),
    refreshButton: document.querySelector("#refresh-button"),
    searchForm: document.querySelector("#search-form"),
    searchInput: document.querySelector("#search-input"),
    statusMessage: document.querySelector("#status-message"),
    tableCaption: document.querySelector("#table-caption"),
    totalSize: document.querySelector("#total-size"),
    transferDialog: document.querySelector("#transfer-dialog"),
    transferForm: document.querySelector("#transfer-form"),
    transferSourceLabel: document.querySelector("#transfer-source-label"),
    transferSubmit: document.querySelector("#transfer-submit"),
    transferTitle: document.querySelector("#transfer-title"),
    uploadButton: document.querySelector("#upload-button"),
    uploadInput: document.querySelector("#upload-input")
};

const state = {
    currentPath: "",
    loadVersion: 0,
    query: "",
    pendingDelete: null,
    pendingTransfer: null
};

function readRoute() {
    const query = new URLSearchParams(window.location.search);
    return {
        path: query.get("path") || "",
        search: query.get("q") || ""
    };
}

function navigate(path, search = "", replace = false) {
    const query = new URLSearchParams();
    if (path) query.set("path", path);
    if (search) query.set("q", search);
    const url = `${window.location.pathname}${query.size ? `?${query}` : ""}`;
    window.history[replace ? "replaceState" : "pushState"]({}, "", url);
    void loadView();
}

async function loadView() {
    const loadVersion = ++state.loadVersion;
    const route = readRoute();
    state.currentPath = route.path;
    state.query = route.search;
    elements.searchInput.value = route.search;
    elements.clearSearchButton.hidden = !route.search;
    setStatus(route.search ? "Searching…" : "Loading folder…");

    try {
        const response = route.search
            ? await filesApi.search(route.path, route.search)
            : await filesApi.browse(route.path);
        if (loadVersion !== state.loadVersion) return false;
        state.currentPath = response.path;
        renderBreadcrumbs(response.path);
        renderSummary(response.summary);
        renderEntries(response.entries);
        const viewName = response.path || "Home";
        elements.tableCaption.textContent = route.search
            ? `Search results for “${response.query}” in ${viewName}`
            : `Contents of ${viewName}`;
        const truncation = response.isTruncated ? " Result limit reached." : "";
        const entryLabel = response.entries.length === 1 ? "entry" : "entries";
        setStatus(`${response.entries.length} ${entryLabel} shown.${truncation}`);
        return true;
    } catch (error) {
        if (loadVersion !== state.loadVersion) return false;
        handleError(error);
        return false;
    }
}

function renderBreadcrumbs(path) {
    elements.breadcrumbs.replaceChildren();
    const segments = path ? path.split("/") : [];
    addBreadcrumb("Home", "");
    let accumulatedPath = "";
    for (const segment of segments) {
        const separator = document.createElement("span");
        separator.className = "breadcrumb-separator";
        separator.textContent = "/";
        separator.setAttribute("aria-hidden", "true");
        elements.breadcrumbs.append(separator);

        accumulatedPath = accumulatedPath ? `${accumulatedPath}/${segment}` : segment;
        addBreadcrumb(segment, accumulatedPath);
    }
}

function addBreadcrumb(label, path) {
    const link = document.createElement("a");
    link.href = routeUrl(path);
    link.textContent = label;
    link.addEventListener("click", event => {
        event.preventDefault();
        navigate(path);
    });
    elements.breadcrumbs.append(link);
}

function renderSummary(summary) {
    elements.folderCount.textContent = summary.folderCount.toLocaleString();
    elements.folderLabel.textContent = summary.folderCount === 1 ? "Folder" : "Folders";
    elements.fileCount.textContent = summary.fileCount.toLocaleString();
    elements.fileLabel.textContent = summary.fileCount === 1 ? "File" : "Files";
    elements.totalSize.textContent = formatBytes(summary.totalFileBytes);
}

function renderEntries(entries) {
    elements.entryList.replaceChildren();
    elements.emptyMessage.hidden = entries.length !== 0;
    for (const entry of entries) {
        elements.entryList.append(createEntryRow(entry));
    }
}

function createEntryRow(entry) {
    const row = document.createElement("tr");
    const nameCell = document.createElement("td");
    if (entry.type === "folder") {
        const openButton = document.createElement("button");
        openButton.type = "button";
        openButton.className = "entry-name-button";
        openButton.textContent = `📁 ${entry.name}`;
        openButton.addEventListener("click", () => navigate(entry.path));
        nameCell.append(openButton);
    } else {
        const downloadLink = document.createElement("a");
        downloadLink.className = "entry-link";
        downloadLink.href = filesApi.downloadUrl(entry.path);
        downloadLink.textContent = `📄 ${entry.name}`;
        downloadLink.setAttribute("download", entry.name);
        nameCell.append(downloadLink);
    }

    const typeCell = document.createElement("td");
    typeCell.className = "entry-type";
    typeCell.textContent = entry.type;
    const sizeCell = document.createElement("td");
    sizeCell.textContent = entry.sizeBytes === null ? "—" : formatBytes(entry.sizeBytes);
    const modifiedCell = document.createElement("td");
    modifiedCell.textContent = new Date(entry.modifiedUtc).toLocaleString();
    const actionCell = document.createElement("td");
    actionCell.className = "entry-actions";
    actionCell.append(
        createActionButton("Copy", entry, () => openTransferDialog("copy", entry)),
        createActionButton("Move", entry, () => openTransferDialog("move", entry)),
        createActionButton("Delete", entry, () => openDeleteDialog(entry), true)
    );
    row.append(nameCell, typeCell, sizeCell, modifiedCell, actionCell);
    return row;
}

function createActionButton(label, entry, action, isDanger = false) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `action-button${isDanger ? " action-button-danger" : ""}`;
    button.textContent = label;
    button.setAttribute("aria-label", `${label} ${entry.type} ${entry.name}`);
    button.addEventListener("click", action);
    return button;
}

function openTransferDialog(operation, entry) {
    state.pendingTransfer = { operation, entry };
    const action = operation === "copy" ? "Copy" : "Move";
    elements.transferTitle.textContent = `${action} ${entry.type}`;
    elements.transferSubmit.textContent = action;
    elements.transferSourceLabel.textContent = `${action} “${entry.path}” to another folder or name.`;
    elements.destinationDirectory.value = state.currentPath;
    elements.destinationName.value = operation === "copy"
        ? copyName(entry.name, entry.type)
        : entry.name;
    elements.transferDialog.showModal();
    elements.destinationDirectory.focus();
}

function openDeleteDialog(entry) {
    state.pendingDelete = entry;
    elements.deleteSourceLabel.textContent = entry.path;
    elements.recursiveDelete.checked = false;
    elements.recursiveDeleteLabel.hidden = entry.type !== "folder";
    elements.deleteDialog.showModal();
}

async function runMutation(action, successMessage) {
    setStatus("Applying filesystem change…");
    try {
        await action();
        if (await loadView()) {
            setStatus(successMessage, "success");
        }
    } catch (error) {
        handleError(error);
        if (error instanceof ApiError && error.status === 404) {
            // A selected source may have changed after rendering; refresh stale UI.
            const staleMessage = error.message;
            if (await loadView()) {
                setStatus(`${staleMessage} The view was refreshed.`, "error");
            }
        }
    }
}

function handleError(error) {
    const message = error instanceof ApiError
        ? error.message
        : "The request could not be completed. Check the server and try again.";
    setStatus(message, "error");
    if (!(error instanceof ApiError)) {
        console.error(error);
    }
}

function setStatus(message, kind = "info") {
    elements.statusMessage.textContent = message;
    elements.statusMessage.dataset.kind = kind;
}

function routeUrl(path) {
    const query = new URLSearchParams();
    if (path) query.set("path", path);
    return `${window.location.pathname}${query.size ? `?${query}` : ""}`;
}

function copyName(name, type) {
    if (type === "folder") return `${name} copy`;
    const dotIndex = name.lastIndexOf(".");
    if (dotIndex <= 0) return `${name} copy`;
    return `${name.slice(0, dotIndex)} copy${name.slice(dotIndex)}`;
}

function formatBytes(bytes) {
    if (bytes === 0) return "0 B";
    const units = ["B", "KB", "MB", "GB", "TB"];
    const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
    const value = bytes / (1024 ** index);
    return `${value.toLocaleString(undefined, { maximumFractionDigits: index ? 1 : 0 })} ${units[index]}`;
}

elements.searchForm.addEventListener("submit", event => {
    event.preventDefault();
    const query = elements.searchInput.value.trim();
    if (query) navigate(state.currentPath, query);
});

elements.clearSearchButton.addEventListener("click", () => navigate(state.currentPath));
elements.refreshButton.addEventListener("click", () => void loadView());
elements.createFolderButton.addEventListener("click", () => {
    elements.createParentLabel.textContent = state.currentPath || "Home";
    elements.createFolderForm.reset();
    elements.createFolderDialog.showModal();
    elements.folderName.focus();
});

elements.createFolderForm.addEventListener("submit", event => {
    if (event.submitter?.value !== "submit") return;
    event.preventDefault();
    const name = elements.folderName.value.trim();
    elements.createFolderDialog.close();
    void runMutation(
        () => filesApi.createFolder(state.currentPath, name),
        `Created folder “${name}”.`
    );
});

elements.transferForm.addEventListener("submit", event => {
    if (event.submitter?.value !== "submit" || !state.pendingTransfer) return;
    event.preventDefault();
    const { operation, entry } = state.pendingTransfer;
    const destination = elements.destinationDirectory.value.trim();
    const newName = elements.destinationName.value.trim();
    elements.transferDialog.close();
    void runMutation(
        () => filesApi.transfer(operation, entry.path, destination, newName),
        `${operation === "copy" ? "Copied" : "Moved"} “${entry.name}”.`
    );
});

elements.deleteForm.addEventListener("submit", event => {
    if (event.submitter?.value !== "submit" || !state.pendingDelete) return;
    event.preventDefault();
    const entry = state.pendingDelete;
    const recursive = entry.type === "folder" && elements.recursiveDelete.checked;
    elements.deleteDialog.close();
    void runMutation(
        () => filesApi.delete(entry.path, recursive),
        `Deleted “${entry.name}”.`
    );
});

elements.uploadButton.addEventListener("click", () => elements.uploadInput.click());
elements.uploadInput.addEventListener("change", () => {
    const [file] = elements.uploadInput.files;
    if (!file) return;
    const overwrite = elements.overwriteUpload.checked;
    void runMutation(
        () => filesApi.upload(state.currentPath, file, overwrite),
        `Uploaded “${file.name}”.`
    );
    elements.uploadInput.value = "";
});

window.addEventListener("popstate", () => void loadView());
void loadView();
