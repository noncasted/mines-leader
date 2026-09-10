var LibraryProfilerTraceDownload = {
    // Файл трассы живёт в MEMFS и в IndexedDB не синхронизируется, а строка в консоли
    // обрезается — поэтому держим копию последней трассы в JS и отдаём её скачиванием
    // по вызову из DevTools: downloadProfilerTrace()
    ProfilerTraceStore: function (namePtr, jsonPtr) {
        const name = UTF8ToString(namePtr);
        const json = UTF8ToString(jsonPtr);

        window.downloadProfilerTrace = function () {
            const a = document.createElement('a');
            a.href = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
            a.download = name;
            a.click();
            setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
        };
    }
};

mergeInto(LibraryManager.library, LibraryProfilerTraceDownload);
