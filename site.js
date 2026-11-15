(function () {
  'use strict';

  // Ask before destructive actions (forms with data-confirm="message").
  document.querySelectorAll('form[data-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      if (!window.confirm(form.getAttribute('data-confirm'))) e.preventDefault();
    });
  });

  // Show the organiser code box only when registering as an Organiser.
  var roleInputs = document.querySelectorAll('input[name="Role"]');
  var codeBox = document.getElementById('organiser-code-box');
  function syncRole() {
    if (!codeBox) return;
    var chosen = document.querySelector('input[name="Role"]:checked');
    codeBox.hidden = !(chosen && chosen.value === 'Organiser');
  }
  roleInputs.forEach(function (r) { r.addEventListener('change', syncRole); });
  syncRole();

  // Preview a chosen image before uploading it.
  document.querySelectorAll('input[type="file"][data-preview]').forEach(function (input) {
    input.addEventListener('change', function () {
      var target = document.getElementById(input.getAttribute('data-preview'));
      var file = input.files && input.files[0];
      if (target && file && file.type.indexOf('image/') === 0) {
        target.src = URL.createObjectURL(file);
      }
    });
  });

  // Fade out success messages after a few seconds (errors stay until dismissed).
  document.querySelectorAll('.alert-success').forEach(function (el) {
    setTimeout(function () { el.classList.remove('show'); }, 6000);
  });
})();
