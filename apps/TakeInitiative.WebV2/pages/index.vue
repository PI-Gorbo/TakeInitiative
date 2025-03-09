<template>
  <main class="w-full h-full bg-background bg-grid">

  </main>
</template>

<script setup lang="ts">

</script>


<style scoped>
@keyframes scrollGrid {
    0% {
    background-position: 0 0;
    }
    100% {
    background-position: 50% 50%; /* Match grid size here */
    }
}

.bg-grid {
  background-image:
    linear-gradient(to bottom, rgba(255, 255, 255, 0.1) 1px, transparent 1px),
    linear-gradient(to right, rgba(255, 255, 255, 0.1) 1px, transparent 1px);
  background-size: 50px 50px; /* Adjust grid size here */
  animation: scrollGrid 50s linear infinite; /* Adjust animation speed here */
  background-color: #121212; /* Or your desired background color */
}

</style>