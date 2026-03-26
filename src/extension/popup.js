/** @file popup.js – Profile picker UI. */

const statusEl = document.getElementById("status");
const listEl = document.getElementById("profile-list");

async function init() {
  const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
  if (!tab?.url) {
    statusEl.textContent = "No active tab.";
    return;
  }

  let profiles;
  try {
    profiles = await browser.runtime.sendMessage({ action: "getProfiles" });
  } catch (err) {
    statusEl.textContent = "Error loading profiles.";
    console.error(err);
    return;
  }

  if (!profiles?.length) {
    statusEl.textContent = "No other profiles detected.";
    return;
  }

  statusEl.hidden = true;
  listEl.hidden = false;

  for (const profile of profiles) {
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.textContent = profile.name;
    btn.style.setProperty("--theme-bg", profile.themeColor ?? "#333");
    btn.addEventListener("click", () => sendTab(tab, profile.name));
    li.appendChild(btn);
    listEl.appendChild(li);
  }
}

async function sendTab(tab, targetProfile) {
  await browser.runtime.sendMessage({
    action: "sendTab",
    targetProfile,
    payload: {
      url: tab.url,
      title: tab.title ?? "",
      pinned: tab.pinned ?? false,
      group_id: null,
    },
  });
  window.close();
}

init();
