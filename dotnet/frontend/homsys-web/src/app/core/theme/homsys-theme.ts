import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';

export const HOMSysTheme = definePreset(Aura, {
  primitive: {
    borderRadius: {
      none: '0',
      xs:   '3px',
      sm:   '3px',
      md:   '3px',
      lg:   '3px',
      xl:   '3px',
    },
  },
  semantic: {
    primary: {
      50:  '#fff0f0',
      100: '#ffe0e0',
      200: '#ffc0c0',
      300: '#ff8888',
      400: '#f55050',
      500: '#e02020',
      600: '#c01010',
      700: '#9a0a0a',
      800: '#800000',
      900: '#620000',
      950: '#3d0000',
    },
  },
});
