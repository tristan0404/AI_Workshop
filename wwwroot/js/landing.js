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

const stackCards = document.querySelectorAll("[data-scroll-stack-card]");
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

if (reduceMotion || !("IntersectionObserver" in window)) {
  stackCards.forEach((card) => card.setAttribute("data-stack-visible", ""));
} else {
  const stackObserver = new IntersectionObserver((entries, observer) => {
    entries.forEach((entry) => {
      if (!entry.isIntersecting) return;
      entry.target.setAttribute("data-stack-visible", "");
      observer.unobserve(entry.target);
    });
  }, { rootMargin: "0px 0px -12%", threshold: 0.12 });

  stackCards.forEach((card) => stackObserver.observe(card));
}
