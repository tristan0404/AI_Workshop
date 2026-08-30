document.querySelectorAll("[data-live-attendance]").forEach((container) => {
  const sessionId = container.dataset.sessionId;
  const refreshSeconds = Number(container.dataset.refreshSeconds) || 30;
  const qrImage = container.querySelector("[data-qr-image]");
  const countdown = container.querySelector("[data-countdown]");
  const closesAt = new Date(countdown?.dataset.closesAt ?? "");

  const refreshQrCode = () => {
    if (qrImage) qrImage.src = `?handler=Qr&id=${sessionId}&t=${Date.now()}`;
  };

  const refreshStatus = async () => {
    try {
      const response = await fetch(`?handler=Status&id=${sessionId}`, { headers: { Accept: "application/json" }, cache: "no-store" });
      if (!response.ok) return;
      const data = await response.json();
      const percentage = data.total === 0 ? 0 : Math.round((data.checkedIn / data.total) * 100);
      container.querySelector("[data-checked-count]").textContent = data.checkedIn;
      container.querySelector("[data-total-count]").textContent = data.total;
      container.querySelector("[data-percentage]").textContent = `${percentage}%`;
      container.querySelector("[data-progress]").style.width = `${percentage}%`;
      data.records.forEach((record) => {
        const row = [...container.querySelectorAll("[data-student-number]")].find((item) => item.dataset.studentNumber === record.studentNumber);
        const action = row?.querySelector("form");
        if (!row || !action) return;
        const chip = document.createElement("span");
        chip.className = "attendance-chip";
        chip.textContent = record.status;
        action.replaceWith(chip);
      });
    } catch { /* The next poll retries without disrupting the register. */ }
  };

  const updateCountdown = () => {
    if (!countdown || Number.isNaN(closesAt.valueOf())) return;
    const remainingSeconds = Math.max(0, Math.ceil((closesAt.valueOf() - Date.now()) / 1000));
    const minutes = Math.floor(remainingSeconds / 60).toString().padStart(2, "0");
    const seconds = (remainingSeconds % 60).toString().padStart(2, "0");
    countdown.textContent = `${minutes}:${seconds}`;
    if (remainingSeconds === 0) window.location.reload();
  };

  updateCountdown();
  window.setInterval(updateCountdown, 1000);
  window.setInterval(refreshQrCode, refreshSeconds * 1000);
  window.setInterval(refreshStatus, 5000);
});
