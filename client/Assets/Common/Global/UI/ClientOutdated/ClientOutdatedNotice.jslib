mergeInto(LibraryManager.library,
    {
        // Loads the page again: the new build after a deploy comes with it.
        ClientOutdatedReload: function () {
            window.location.reload();
        }
    });
