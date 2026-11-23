/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  output: "standalone",
  eslint: {
    ignoreDuringBuilds: true,
  },
  typescript: {
    ignoreBuildErrors: true,
  },
  experimental: {
    serverComponentsExternalPackages: ["bcryptjs", "@supabase/supabase-js"],
  },
  async rewrites() {
    return [
      { source: '/gazyava', destination: '/gazyava/index.html' },
      { source: '/gazyava/', destination: '/gazyava/index.html' },
    ];
  },
};

export default nextConfig;
