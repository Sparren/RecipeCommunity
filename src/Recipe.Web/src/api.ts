export async function api<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const response = await fetch("/api" + path, {
    ...options,
    headers: {
      ...(options.body instanceof FormData
        ? {}
        : { "Content-Type": "application/json" }),
      ...options.headers,
    },
  });
  if (!response.ok) {
    const error = await response
      .json()
      .catch(() => ({ title: "Servern kunde inte nås." }));
    throw new Error(
      error.errors
        ? Object.entries(error.errors)
            .map(
              ([field, messages]) =>
                `${field}: ${(messages as string[]).join(" ")}`,
            )
            .join("\n")
        : error.title || `Fel ${response.status}`,
    );
  }
  return response.status === 204
    ? (undefined as T)
    : ((await response.json()) as T);
}
