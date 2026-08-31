const chartRoot = document.querySelector("[data-dashboard-charts]");

if (chartRoot) {
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const svgNamespace = "http://www.w3.org/2000/svg";
  const readData = (name) => JSON.parse(chartRoot.querySelector(`[data-chart-data="${name}"]`)?.textContent || "[]");
  const svgElement = (name, attributes = {}) => {
    const element = document.createElementNS(svgNamespace, name);
    Object.entries(attributes).forEach(([key, value]) => element.setAttribute(key, value));
    return element;
  };
  const empty = (host, message) => {
    const state = document.createElement("div");
    state.className = "chart-empty";
    state.textContent = message;
    host.append(state);
  };
  const tooltip = (host) => {
    let element = host.querySelector(".chart-tooltip");
    if (!element) { element = document.createElement("div"); element.className = "chart-tooltip"; host.append(element); }
    return element;
  };
  const showTooltip = (host, event, title, rows) => {
    const tip = tooltip(host);
    tip.replaceChildren();
    const heading = document.createElement("strong"); heading.textContent = title; tip.append(heading);
    rows.forEach(([label, value]) => { const row = document.createElement("span"); row.textContent = `${label}  ${value}`; tip.append(row); });
    const bounds = host.getBoundingClientRect();
    tip.style.left = `${Math.min(bounds.width - 150, Math.max(8, event.clientX - bounds.left + 12))}px`;
    tip.style.top = `${Math.max(8, event.clientY - bounds.top - 74)}px`;
    tip.classList.add("is-visible");
  };
  const hideTooltip = (host) => tooltip(host).classList.remove("is-visible");

  const renderLine = () => {
    const host = chartRoot.querySelector("[data-line-chart]");
    const data = readData("line");
    if (!host || data.length === 0) { if (host) empty(host, "Attendance trends appear after your first past lecture."); return; }
    const width = 800, height = 260, left = 45, right = 20, top = 18, bottom = 38;
    const plotWidth = width - left - right, plotHeight = height - top - bottom;
    const svg = svgElement("svg", { viewBox: `0 0 ${width} ${height}`, role: "img" });
    [0, 25, 50, 75, 100].forEach(value => {
      const y = top + plotHeight - (value / 100) * plotHeight;
      svg.append(svgElement("line", { x1: left, x2: width - right, y1: y, y2: y, class: "chart-grid-line" }));
      const label = svgElement("text", { x: left - 12, y: y + 4, class: "chart-axis-label chart-axis-label-y", "text-anchor": "end" }); label.textContent = `${value}%`; svg.append(label);
    });
    const points = data.map((point, index) => ({ ...point, x: left + (data.length === 1 ? plotWidth / 2 : index * plotWidth / (data.length - 1)), y: top + plotHeight - (Number(point.value) / 100) * plotHeight }));
    const areaPoints = `${left},${top + plotHeight} ${points.map(point => `${point.x},${point.y}`).join(" ")} ${width - right},${top + plotHeight}`;
    svg.append(svgElement("polygon", { points: areaPoints, class: "trend-area" }));
    svg.append(svgElement("polyline", { points: points.map(point => `${point.x},${point.y}`).join(" "), class: "trend-line" }));
    points.forEach((point, index) => {
      if (index === 0 || index === points.length - 1 || index % Math.ceil(points.length / 5) === 0) { const label = svgElement("text", { x: point.x, y: height - 9, class: "chart-axis-label chart-axis-label-x", "text-anchor": "middle" }); label.textContent = point.label; svg.append(label); }
      const crosshair = svgElement("line", { x1: point.x, x2: point.x, y1: top, y2: top + plotHeight, class: "chart-crosshair" }); svg.append(crosshair);
      const dot = svgElement("circle", { cx: point.x, cy: point.y, r: 6, class: "trend-point", tabindex: "0" });
      const reveal = event => { svg.querySelectorAll(".chart-crosshair").forEach(item => item.classList.remove("is-active")); crosshair.classList.add("is-active"); showTooltip(host, event, point.label, [[point.detail, `${point.value}%`]]); };
      dot.addEventListener("pointerenter", reveal); dot.addEventListener("pointermove", reveal); dot.addEventListener("pointerleave", () => { crosshair.classList.remove("is-active"); hideTooltip(host); });
      dot.addEventListener("focus", event => reveal({ clientX: host.getBoundingClientRect().left + point.x * host.clientWidth / width, clientY: host.getBoundingClientRect().top + 90 })); dot.addEventListener("blur", () => hideTooltip(host));
      svg.append(dot);
    });
    host.append(svg);
    const reveal = document.createElement("span"); reveal.className = "chart-reveal"; host.append(reveal);
    requestAnimationFrame(() => { if (!reduceMotion) reveal.classList.add("is-revealed"); else reveal.remove(); });
  };

  const renderBars = () => {
    const host = chartRoot.querySelector("[data-bar-chart]");
    const data = readData("bar");
    if (!host || data.length === 0) { if (host) empty(host, "Lecture comparisons will appear when attendance is recorded."); return; }
    const max = Math.max(1, ...data.flatMap(point => [point.attended, point.missed]));
    const grid = document.createElement("div"); grid.className = "bar-chart-grid";
    data.forEach((point, index) => {
      const group = document.createElement("div"); group.className = "bar-group";
      const bars = document.createElement("div"); bars.className = "bar-pair";
      [["Attended", point.attended, "bar-attended"], ["Missed", point.missed, "bar-missed"]].forEach(([label, value, className]) => {
        const bar = document.createElement("button"); bar.type = "button"; bar.className = `chart-bar ${className}`;
        bar.style.setProperty("--bar-scale", String(Number(value) / max)); bar.style.setProperty("--bar-delay", `${index * 30}ms`); bar.setAttribute("aria-label", `${point.detail}, ${point.label}: ${value} ${label.toLowerCase()}`);
        const reveal = event => showTooltip(host, event, `${point.detail} · ${point.label}`, [[label, value]]);
        bar.addEventListener("pointerenter", reveal); bar.addEventListener("pointermove", reveal); bar.addEventListener("pointerleave", () => hideTooltip(host)); bar.addEventListener("focus", event => reveal({ clientX: event.target.getBoundingClientRect().left, clientY: event.target.getBoundingClientRect().top })); bar.addEventListener("blur", () => hideTooltip(host));
        bars.append(bar);
      });
      const label = document.createElement("span"); label.textContent = point.label; group.append(bars, label); grid.append(group);
    });
    host.append(grid); requestAnimationFrame(() => host.classList.add("is-ready"));
  };

  const renderDonut = () => {
    const host = chartRoot.querySelector("[data-donut-chart]");
    const legend = chartRoot.querySelector("[data-donut-legend]");
    const data = readData("donut");
    const total = data.reduce((sum, point) => sum + Number(point.value), 0);
    if (!host || !legend || total === 0) { if (host) empty(host, "No attendance statuses to display yet."); return; }
    const colors = ["#f5cb5c", "#879b91", "#333533", "#c9a66b"];
    const radius = 74, circumference = 2 * Math.PI * radius;
    const svg = svgElement("svg", { viewBox: "0 0 200 200", role: "img" });
    svg.append(svgElement("circle", { cx: 100, cy: 100, r: radius, class: "donut-track" }));
    let offset = 0;
    data.forEach((point, index) => {
      const fraction = Number(point.value) / total;
      const segment = svgElement("circle", { cx: 100, cy: 100, r: radius, class: "donut-segment", stroke: colors[index], "stroke-dasharray": `${fraction * circumference} ${circumference}`, "stroke-dashoffset": -offset, tabindex: "0" });
      offset += fraction * circumference;
      const reveal = event => { svg.querySelectorAll(".donut-segment").forEach(item => item.classList.remove("is-active")); segment.classList.add("is-active"); showTooltip(host, event, point.label, [["Records", point.value], ["Share", `${Math.round(fraction * 100)}%`]]); };
      segment.addEventListener("pointerenter", reveal); segment.addEventListener("pointermove", reveal); segment.addEventListener("pointerleave", () => { segment.classList.remove("is-active"); hideTooltip(host); }); segment.addEventListener("focus", event => reveal({ clientX: host.getBoundingClientRect().left + 110, clientY: host.getBoundingClientRect().top + 80 })); segment.addEventListener("blur", () => hideTooltip(host)); svg.append(segment);
      const row = document.createElement("button"); row.type = "button"; row.className = "donut-legend-row"; row.innerHTML = `<i style="--legend-color:${colors[index]}"></i><span>${point.label}</span><strong>${point.value}</strong>`;
      row.addEventListener("pointerenter", () => segment.classList.add("is-active")); row.addEventListener("pointerleave", () => segment.classList.remove("is-active")); legend.append(row);
    });
    const value = svgElement("text", { x: 100, y: 96, class: "donut-value", "text-anchor": "middle" }); value.textContent = total; svg.append(value);
    const label = svgElement("text", { x: 100, y: 116, class: "donut-label", "text-anchor": "middle" }); label.textContent = "records"; svg.append(label);
    host.append(svg); requestAnimationFrame(() => host.classList.add("is-ready"));
  };

  const renderHeatmap = () => {
    const host = chartRoot.querySelector("[data-heatmap-chart]");
    const data = readData("heatmap");
    if (!host) return;
    const values = new Map(data.map(point => [point.date, point]));
    const end = new Date(); end.setHours(0, 0, 0, 0);
    const start = new Date(end); start.setDate(end.getDate() - 363 - end.getDay());
    const grid = document.createElement("div"); grid.className = "heatmap-grid";
    for (let day = new Date(start), index = 0; day <= end; day.setDate(day.getDate() + 1), index++) {
      const key = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, "0")}-${String(day.getDate()).padStart(2, "0")}`;
      const point = values.get(key); const level = !point ? 0 : Math.max(1, Math.min(4, Math.ceil(Number(point.value) / 25)));
      const cell = document.createElement("button"); cell.type = "button"; cell.className = "heatmap-cell"; cell.dataset.level = String(level); cell.style.setProperty("--cell-delay", `${(index % 18) * 12}ms`);
      cell.setAttribute("aria-label", `${day.toLocaleDateString(undefined, { dateStyle: "long" })}: ${point ? `${point.value}% attendance across ${point.sessions} lecture${point.sessions === 1 ? "" : "s"}` : "no lectures"}`);
      const reveal = event => showTooltip(host, event, day.toLocaleDateString(undefined, { dateStyle: "long" }), point ? [["Attendance", `${point.value}%`], ["Lectures", point.sessions]] : [["Lectures", 0]]);
      cell.addEventListener("pointerenter", reveal); cell.addEventListener("pointermove", reveal); cell.addEventListener("pointerleave", () => hideTooltip(host)); grid.append(cell);
    }
    host.append(grid); requestAnimationFrame(() => host.classList.add("is-ready"));
  };

  renderLine(); renderBars(); renderDonut(); renderHeatmap();
}
