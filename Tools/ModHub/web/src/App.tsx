import { Navigate, Route, Routes } from 'react-router-dom';
import { TopBar } from './components/layout/TopBar';
import { Footer } from './components/layout/Footer';
import BrowsePage from './pages/BrowsePage';
import ModDetailPage from './pages/ModDetailPage';
import UploadPage from './pages/UploadPage';
import LoginPage from './pages/LoginPage';
import NotFoundPage from './pages/NotFoundPage';
import MyWorkshopPage from './pages/MyWorkshopPage';
import AdminShell from './components/admin/AdminShell';
import AdminOverviewPage from './pages/admin/AdminOverviewPage';
import AdminUsersPage from './pages/admin/AdminUsersPage';
import AdminModsPage from './pages/admin/AdminModsPage';

/**
 * 路由表
 *
 * 路径刻意与 Steam 创意工坊保持一致（`/sharedfiles/filedetails/?id=`），
 * 老玩家凭肌肉记忆即可拼出详情页地址，便于社区分享。
 */
export default function App() {
  return (
    <div className="flex min-h-full flex-col">
      <TopBar />
      <main className="flex-1">
        <Routes>
          <Route path="/" element={<Navigate to="/browse" replace />} />
          <Route path="/browse" element={<BrowsePage />} />
          <Route path="/sharedfiles/filedetails" element={<ModDetailPage />} />
          <Route path="/upload" element={<UploadPage />} />
          <Route path="/my" element={<MyWorkshopPage />} />
          <Route path="/login" element={<LoginPage />} />

          {/* 管理后台：外壳负责权限守卫与侧边导航，子路由只关心各自的内容 */}
          <Route path="/admin" element={<AdminShell />}>
            <Route index element={<AdminOverviewPage />} />
            <Route path="users" element={<AdminUsersPage />} />
            <Route path="mods" element={<AdminModsPage />} />
          </Route>

          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
      <Footer />
    </div>
  );
}
