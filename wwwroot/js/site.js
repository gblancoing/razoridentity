// Menú móvil: toggle del navbar en pantallas pequeñas
(function () {
  var toggle = document.getElementById('nav-toggle');
  var menu = document.getElementById('navbar-menu');
  if (!toggle || !menu) return;

  toggle.addEventListener('click', function () {
    menu.classList.toggle('hidden');
    var expanded = menu.classList.contains('hidden') ? 'false' : 'true';
    toggle.setAttribute('aria-expanded', expanded);
  });
})();
