// ═════════ Smooth scroll with Lenis ═════════
const lenis = new Lenis({
  duration: 1.2,
  easing: (t) => Math.min(1, 1.001 - Math.pow(2, -10 * t)),
  smoothWheel: true,
});

function raf(time) {
  lenis.raf(time);
  requestAnimationFrame(raf);
}
requestAnimationFrame(raf);

// ═════════ GSAP ScrollTrigger setup ═════════
gsap.registerPlugin(ScrollTrigger);

// Connect Lenis to ScrollTrigger
lenis.on('scroll', ScrollTrigger.update);
gsap.ticker.add((time) => lenis.raf(time * 1000));
gsap.ticker.lagSmoothing(0);

// ═════════ Hero parallax — landscape moves on scroll ═════════
gsap.to('.sun', {
  y: 150,
  scale: 0.8,
  scrollTrigger: {
    trigger: '.hero',
    start: 'top top',
    end: 'bottom top',
    scrub: 1,
  }
});

gsap.to('.mountain-back', {
  y: 80,
  scrollTrigger: {
    trigger: '.hero',
    start: 'top top',
    end: 'bottom top',
    scrub: 1,
  }
});

gsap.to('.mountain-mid', {
  y: 60,
  scrollTrigger: {
    trigger: '.hero',
    start: 'top top',
    end: 'bottom top',
    scrub: 1,
  }
});

gsap.to('.mountain-front', {
  y: 40,
  scrollTrigger: {
    trigger: '.hero',
    start: 'top top',
    end: 'bottom top',
    scrub: 1,
  }
});

gsap.to('.grass', {
  y: 20,
  scrollTrigger: {
    trigger: '.hero',
    start: 'top top',
    end: 'bottom top',
    scrub: 1,
  }
});

// ═════════ App preview 3D rotation on scroll ═════════
gsap.fromTo('.preview',
  {
    rotateX: 25,
    scale: 0.95,
  },
  {
    rotateX: 0,
    scale: 1,
    scrollTrigger: {
      trigger: '.preview-wrap',
      start: 'top 80%',
      end: 'top 30%',
      scrub: 1,
    }
  }
);

// ═════════ App preview mouse tilt ═════════
const preview = document.querySelector('.preview');
const previewWrap = document.querySelector('.preview-wrap');

if (preview && previewWrap) {
  previewWrap.addEventListener('mousemove', (e) => {
    const rect = previewWrap.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width - 0.5;
    const y = (e.clientY - rect.top) / rect.height - 0.5;

    preview.style.transform = `
      perspective(1800px)
      rotateY(${x * 8}deg)
      rotateX(${-y * 8}deg)
      translateZ(0)
    `;
  });

  previewWrap.addEventListener('mouseleave', () => {
    preview.style.transform = 'perspective(1800px) rotateY(0) rotateX(0)';
  });
}

// ═════════ Reveal on scroll ═════════
const observer = new IntersectionObserver((entries) => {
  entries.forEach((entry, i) => {
    if (entry.isIntersecting) {
      setTimeout(() => {
        entry.target.classList.add('revealed');
      }, i * 80);
      observer.unobserve(entry.target);
    }
  });
}, {
  threshold: 0.15,
  rootMargin: '0px 0px -50px 0px'
});

document.querySelectorAll('[data-reveal]').forEach(el => observer.observe(el));

// ═════════ Hero title fade on scroll ═════════
gsap.to('.hero-inner', {
  opacity: 0,
  y: -50,
  scrollTrigger: {
    trigger: '.hero',
    start: 'top top',
    end: '+=400',
    scrub: 1,
  }
});

// ═════════ Nav blur intensifies on scroll ═════════
const nav = document.querySelector('.nav-pill');
window.addEventListener('scroll', () => {
  const scrolled = window.scrollY;
  if (scrolled > 50) {
    nav.style.transform = 'scale(0.96)';
  } else {
    nav.style.transform = 'scale(1)';
  }
});
nav.style.transition = 'transform 0.3s cubic-bezier(0.2, 0.8, 0.2, 1)';