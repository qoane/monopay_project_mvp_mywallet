(function () {
  const token = localStorage.getItem('monopay_token');
  const isAuthenticated = Boolean(token);

  // Hide sandbox links in navigation when the visitor is not authenticated
  document.querySelectorAll('[data-protected-link="sandbox"]').forEach(link => {
    const parent = link.closest('.nav-item');
    if (!isAuthenticated && parent) {
      parent.classList.add('d-none');
    }
  });

  // If the page requires authentication, redirect unauthenticated visitors to login
  const requireAuth = document.body?.dataset.requireAuth === 'true';
  if (requireAuth && !isAuthenticated) {
    const redirectTarget = window.location.pathname.split('/').pop() || 'sandbox.html';
    const params = new URLSearchParams({ redirect: redirectTarget });
    window.location.replace(`login.html?${params.toString()}`);
    return;
  }
})();
