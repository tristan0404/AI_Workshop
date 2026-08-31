const officeHoursDialog = document.querySelector("[data-office-hours-dialog]");

document.querySelectorAll("[data-office-hours-open]").forEach((button) => {
  button.addEventListener("click", () => officeHoursDialog?.showModal());
});

document.querySelectorAll("[data-office-hours-close]").forEach((button) => {
  button.addEventListener("click", () => officeHoursDialog?.close());
});

if (officeHoursDialog?.dataset.openOnLoad === "true") officeHoursDialog.showModal();

officeHoursDialog?.addEventListener("click", (event) => {
  if (event.target === officeHoursDialog) officeHoursDialog.close();
});
