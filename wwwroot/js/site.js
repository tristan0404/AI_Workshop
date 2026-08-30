document.querySelectorAll("[data-sidebar-toggle]").forEach((toggle) => {
  toggle.addEventListener("click", () => document.body.classList.toggle("sidebar-open"));
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") document.body.classList.remove("sidebar-open");
});
