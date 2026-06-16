// ═════════ Reveal animations using IntersectionObserver ═════════
// Much lighter than GSAP — no scroll lag

const revealObserver = new IntersectionObserver((entries) => {
  entries.forEach((entry) => {
    if (entry.isIntersecting) {
      entry.target.classList.add('visible');
      revealObserver.unobserve(entry.target);
    }
  });
}, {
  threshold: 0.12,
  rootMargin: '0px 0px -60px 0px'
});

document.querySelectorAll('.reveal').forEach(el => revealObserver.observe(el));

// ═════════ Mockup mouse tilt — subtle and smooth ═════════
const mockup = document.querySelector('.mockup');
const stage = document.querySelector('.mockup-stage');

if (mockup && stage) {
  let rafId = null;
  let targetX = 0;
  let targetY = 0;
  let currentX = 0;
  let currentY = 0;

  function tick() {
    currentX += (targetX - currentX) * 0.08;
    currentY += (targetY - currentY) * 0.08;

    mockup.style.transform = `
      rotateX(${8 + currentY}deg)
      rotateY(${currentX}deg)
    `;

    if (Math.abs(targetX - currentX) > 0.01 || Math.abs(targetY - currentY) > 0.01) {
      rafId = requestAnimationFrame(tick);
    } else {
      rafId = null;
    }
  }

  stage.addEventListener('mousemove', (e) => {
    const rect = stage.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width - 0.5;
    const y = (e.clientY - rect.top) / rect.height - 0.5;

    targetX = x * 6;
    targetY = -y * 4;

    if (!rafId) rafId = requestAnimationFrame(tick);
  });

  stage.addEventListener('mouseleave', () => {
    targetX = 0;
    targetY = 0;
    if (!rafId) rafId = requestAnimationFrame(tick);
  });
}

// ═════════ Nav scale on scroll — very lightweight ═════════
const nav = document.querySelector('.nav-pill');
let lastScroll = 0;
let ticking = false;

window.addEventListener('scroll', () => {
  if (!ticking) {
    requestAnimationFrame(() => {
      const scrolled = window.scrollY;
      if (scrolled > 50 && lastScroll <= 50) {
        nav.style.transform = 'scale(0.96)';
      } else if (scrolled <= 50 && lastScroll > 50) {
        nav.style.transform = 'scale(1)';
      }
      lastScroll = scrolled;
      ticking = false;
    });
    ticking = true;
  }
}, { passive: true });

nav.style.transition = 'transform 0.3s cubic-bezier(0.2, 0.8, 0.2, 1)';