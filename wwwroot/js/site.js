document.querySelectorAll("[data-sidebar-toggle]").forEach((toggle) => {
  toggle.addEventListener("click", () => document.body.classList.toggle("sidebar-open"));
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") document.body.classList.remove("sidebar-open");
});

const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

document.querySelectorAll("[data-text-type]").forEach((element) => {
  const content = element.querySelector(".text-type__content");
  if (!content || reducedMotion.matches) return;

  const fullText = content.textContent ?? "";
  const typingSpeed = Number(element.dataset.typingSpeed) || 55;
  const initialDelay = Number(element.dataset.initialDelay) || 0;
  const characters = typeof Intl.Segmenter === "function"
    ? [...new Intl.Segmenter(undefined, { granularity: "grapheme" }).segment(fullText)].map(({ segment }) => segment)
    : Array.from(fullText);

  content.textContent = "";
  element.classList.add("is-typing");
  let characterIndex = 0;

  const typeNextCharacter = () => {
    content.textContent += characters[characterIndex];
    characterIndex += 1;

    if (characterIndex < characters.length) {
      window.setTimeout(typeNextCharacter, typingSpeed);
    } else {
      element.classList.remove("is-typing");
    }
  };

  window.setTimeout(typeNextCharacter, initialDelay);
});
