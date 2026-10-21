// Opens and closes a native <dialog> as a modal (used by Shared/ConfirmDialog.razor).
// showModal() is what gives the dialog its focus trapping, Escape to close, and inert background.
window.appDialog = {
    show: (dialog) => { if (!dialog.open) dialog.showModal(); },
    close: (dialog) => { if (dialog.open) dialog.close(); },
};
