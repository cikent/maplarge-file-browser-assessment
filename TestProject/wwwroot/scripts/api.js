const apiRoot = "/api/files";

export class ApiError extends Error {
    constructor(status, title, detail) {
        super(detail || title || `Request failed with status ${status}.`);
        this.name = "ApiError";
        this.status = status;
        this.title = title;
    }
}

async function request(url, options = {}) {
    const response = await fetch(url, options);
    if (!response.ok) {
        let problem = {};
        try {
            problem = await response.json();
        } catch {
            // Preserve a useful status when an intermediary returns non-JSON.
        }

        throw new ApiError(response.status, problem.title, problem.detail);
    }

    return response.status === 204 ? null : response.json();
}

function withQuery(route, values) {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(values)) {
        if (value !== undefined && value !== null && value !== "") {
            query.set(key, value);
        }
    }

    const suffix = query.toString();
    const url = route ? `${apiRoot}/${route}` : apiRoot;
    return `${url}${suffix ? `?${suffix}` : ""}`;
}

export const filesApi = {
    browse(path) {
        return request(withQuery("browse", { path }));
    },

    search(path, query) {
        return request(withQuery("search", { path, query }));
    },

    downloadUrl(path) {
        return withQuery("download", { path });
    },

    createFolder(parentPath, name) {
        return request(`${apiRoot}/folders`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ parentPath, name })
        });
    },

    transfer(operation, sourcePath, destinationDirectory, newName) {
        return request(`${apiRoot}/${operation}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ sourcePath, destinationDirectory, newName })
        });
    },

    delete(path, recursive) {
        return request(withQuery("", { path, recursive }), { method: "DELETE" });
    },

    upload(path, file, overwrite) {
        const form = new FormData();
        form.append("file", file);
        form.append("path", path);
        form.append("overwrite", String(overwrite));
        return request(`${apiRoot}/upload`, { method: "POST", body: form });
    }
};
