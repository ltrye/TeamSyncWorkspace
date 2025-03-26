export function useDocumentExport(documentId) {
    const { ref } = Vue;

    const showExportOptions = ref(false);

    const exportAsPdf = () => {
        window.open(`/api/Documents/${documentId}/export/pdf`, '_blank');
        showExportOptions.value = false;
    };

    const exportAsDocx = () => {
        window.open(`/api/Documents/${documentId}/export/docx`, '_blank');
        showExportOptions.value = false;
    };

    const printDocument = (title, getContent) => {
        const win = window.open('', '_blank');
        if (win) {
            win.document.write(`
        <html>
          <head>
            <title>${title}</title>
            <style>
              body { font-family: Arial, sans-serif; margin: 20px; }
              h1 { margin-bottom: 20px; }
            </style>
          </head>
          <body>
            <h1>${title}</h1>
            ${getContent()}
          </body>
        </html>
      `);
            win.document.close();
            win.focus();
            setTimeout(() => {
                win.print();
            }, 250);
        }
        showExportOptions.value = false;
    };

    return {
        showExportOptions,
        exportAsPdf,
        exportAsDocx,
        printDocument
    };
}