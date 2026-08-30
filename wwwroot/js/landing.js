const landingMenuButton = document.querySelector("[data-landing-menu]");
const landingNavigation = document.querySelector("#landingNavigation");

landingMenuButton?.addEventListener("click", () => {
  const isOpen = landingMenuButton.getAttribute("aria-expanded") === "true";
  landingMenuButton.setAttribute("aria-expanded", String(!isOpen));
  landingNavigation?.classList.toggle("is-open", !isOpen);
});

landingNavigation?.querySelectorAll("a").forEach((link) => {
  link.addEventListener("click", () => {
    landingMenuButton?.setAttribute("aria-expanded", "false");
    landingNavigation.classList.remove("is-open");
  });
});
