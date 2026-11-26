// Saves a file the console built on the server: the browser downloads it from a data url.
export function downloadFileFromBase64(fileName, base64, mimeType) {
    const link = document.createElement('a');
    link.href = 'data:' + mimeType + ';base64,' + base64;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}
