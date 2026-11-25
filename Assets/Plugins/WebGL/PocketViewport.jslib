mergeInto(LibraryManager.library, {
  SetPocketViewport: function (enabled) {
    var canvas = document.querySelector('#unity-canvas');
    if (!canvas) return;
    if (enabled) {
      canvas.style.setProperty('position', 'fixed', 'important');
      canvas.style.setProperty('left', '50%', 'important');
      canvas.style.setProperty('top', '50%', 'important');
      canvas.style.setProperty('transform', 'translate(-50%, -50%)', 'important');
      canvas.style.setProperty('width', 'min(100vw, 56.25vh)', 'important');
      canvas.style.setProperty('height', 'min(100vh, 177.777778vw)', 'important');
      document.body.style.backgroundColor = '#202329';
    } else {
      ['position', 'left', 'top', 'transform', 'width', 'height'].forEach(function (property) {
        canvas.style.removeProperty(property);
      });
      document.body.style.removeProperty('background-color');
    }
  }
});
