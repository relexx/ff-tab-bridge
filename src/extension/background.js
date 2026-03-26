/** @file background.js – NMH connection, context menu, and message routing. */

const NMH_NAME = "tab_bridge";

let port = null;

// ── NMH connection ────────────────────────────────────────────────────────────

function connectNmh() {
  port = browser.runtime.connectNative(NMH_NAME);
  port.onMessage.addListener(onNmhMessage);
  port.onDisconnect.addListener(() => {
    console.error("Tab Bridge: NMH disconnected.", port.error);
    port = null;
  });
}

function onNmhMessage(message) {
  if (message.type === "TAB_SEND" || message.type === "TAB_SEND_BATCH") {
    openReceivedTab(message);
  } else if (message.type === "PROFILE_LIST_RESPONSE") {
    handleProfileListResponse(message);
  }
}

// ── Context menu ──────────────────────────────────────────────────────────────

browser.menus.create({
  id: "send-tab",
  title: "Send tab to profile…",
  contexts: ["tab"],
});

browser.menus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== "send-tab" || !tab) return;
  // Open the popup for profile selection – handled via browser.action.openPopup()
  await browser.action.openPopup();
});

// ── Tab receiving ─────────────────────────────────────────────────────────────

async function openReceivedTab(message) {
  const { url, title, pinned } = message.payload ?? {};
  if (!url) return;

  await browser.tabs.create({ url, pinned: pinned ?? false, active: true });
  browser.notifications.create({
    type: "basic",
    iconUrl: "icons/icon-48.png",
    title: "Tab received",
    message: `From ${message.source_profile}: ${title ?? url}`,
  });
}

// ── Profile list ──────────────────────────────────────────────────────────────

let pendingProfileListResolve = null;

function requestProfileList() {
  return new Promise((resolve) => {
    pendingProfileListResolve = resolve;
    sendMessage({ type: "PROFILE_LIST_REQUEST" });
  });
}

function handleProfileListResponse(message) {
  if (pendingProfileListResolve) {
    pendingProfileListResolve(message.payload);
    pendingProfileListResolve = null;
  }
}

// ── Messaging helpers ─────────────────────────────────────────────────────────

function sendMessage(partial) {
  if (!port) connectNmh();
  const message = {
    version: 1,
    id: crypto.randomUUID(),
    timestamp: Math.floor(Date.now() / 1000),
    source_profile: "",
    target_profile: "",
    payload: null,
    hmac: "",
    ...partial,
  };
  port.postMessage(message);
}

// ── Popup communication ───────────────────────────────────────────────────────

browser.runtime.onMessage.addListener(async (request) => {
  if (request.action === "getProfiles") {
    return requestProfileList();
  }
  if (request.action === "sendTab") {
    sendMessage({
      type: "TAB_SEND",
      target_profile: request.targetProfile,
      payload: request.payload,
    });
    return { ok: true };
  }
});

connectNmh();
